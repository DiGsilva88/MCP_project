Attribute VB_Name = "FlowLink"
Option Explicit
' Botao "Ligar o Flow": assina um link de uso unico (60 s) e abre-o no browser. Ver docs\IIS.md seccao 11.
' Uso:  AbrirFlow "https://flow.exemplo.pt", "cliente\ana", SegredoDoFlow
' O segredo = VENDAS_LINK_SEGREDO (Iniciar-Flow.cmd). Nao o deixar em texto no .exe sem o ofuscar/ler de configuracao.
' Login so com caracteres ASCII (assinatura em ANSI). Nao testado em VB6: validar primeiro com o exemplo PowerShell da seccao 11.

Private Type SYSTEMTIME
    wYear As Integer: wMonth As Integer: wDayOfWeek As Integer: wDay As Integer
    wHour As Integer: wMinute As Integer: wSecond As Integer: wMilliseconds As Integer
End Type
Private Type HMAC_INFO
    HashAlgid As Long: pbInner As Long: cbInner As Long: pbOuter As Long: cbOuter As Long
End Type

Private Declare Sub GetSystemTime Lib "kernel32" (lpSystemTime As SYSTEMTIME)
Private Declare Function ShellExecute Lib "shell32.dll" Alias "ShellExecuteA" (ByVal hwnd As Long, ByVal lpOperation As String, ByVal lpFile As String, ByVal lpParameters As String, ByVal lpDirectory As String, ByVal nShowCmd As Long) As Long
Private Declare Function CryptAcquireContext Lib "advapi32.dll" Alias "CryptAcquireContextA" (ByRef phProv As Long, ByVal pszContainer As String, ByVal pszProvider As String, ByVal dwProvType As Long, ByVal dwFlags As Long) As Long
Private Declare Function CryptImportKey Lib "advapi32.dll" (ByVal hProv As Long, ByRef pbData As Any, ByVal dwDataLen As Long, ByVal hPubKey As Long, ByVal dwFlags As Long, ByRef phKey As Long) As Long
Private Declare Function CryptCreateHash Lib "advapi32.dll" (ByVal hProv As Long, ByVal Algid As Long, ByVal hKey As Long, ByVal dwFlags As Long, ByRef phHash As Long) As Long
Private Declare Function CryptSetHashParam Lib "advapi32.dll" (ByVal hHash As Long, ByVal dwParam As Long, ByRef pbData As Any, ByVal dwFlags As Long) As Long
Private Declare Function CryptHashData Lib "advapi32.dll" (ByVal hHash As Long, ByRef pbData As Any, ByVal dwDataLen As Long, ByVal dwFlags As Long) As Long
Private Declare Function CryptGetHashParam Lib "advapi32.dll" (ByVal hHash As Long, ByVal dwParam As Long, ByRef pbData As Any, ByRef pdwDataLen As Long, ByVal dwFlags As Long) As Long
Private Declare Function CryptDestroyHash Lib "advapi32.dll" (ByVal hHash As Long) As Long
Private Declare Function CryptDestroyKey Lib "advapi32.dll" (ByVal hKey As Long) As Long
Private Declare Function CryptReleaseContext Lib "advapi32.dll" (ByVal hProv As Long, ByVal dwFlags As Long) As Long

Public Sub AbrirFlow(ByVal enderecoBase As String, ByVal login As String, ByVal segredo As String)
    Dim e As Long, s As String
    e = SegundosUnixUtc() + 60
    s = HmacSha256Hex(segredo, login & "|" & e)
    If Len(s) = 0 Then MsgBox "Nao foi possivel assinar o acesso ao Flow.", vbExclamation: Exit Sub
    ShellExecute 0&, "open", enderecoBase & "/?u=" & UrlEnc(login) & "&e=" & e & "&s=" & s, vbNullString, vbNullString, 1
End Sub

Private Function SegundosUnixUtc() As Long
    Dim t As SYSTEMTIME
    GetSystemTime t
    SegundosUnixUtc = DateDiff("s", #1/1/1970#, DateSerial(t.wYear, t.wMonth, t.wDay) + TimeSerial(t.wHour, t.wMinute, t.wSecond))
End Function

Private Function UrlEnc(ByVal v As String) As String
    Dim i As Long, c As String
    For i = 1 To Len(v)
        c = Mid$(v, i, 1)
        If c Like "[A-Za-z0-9._~-]" Then UrlEnc = UrlEnc & c Else UrlEnc = UrlEnc & "%" & Right$("0" & Hex$(Asc(c)), 2)
    Next
End Function

' HMAC-SHA256 pela CryptoAPI (chave importada como RC2 com CRYPT_IPSEC_HMAC_KEY, o truque habitual). Devolve "" se falhar.
Private Function HmacSha256Hex(ByVal chave As String, ByVal msg As String) As String
    Const PROV_RSA_AES = 24, CRYPT_VERIFYCONTEXT = &HF0000000, CALG_RC2 = &H6602, CALG_HMAC = &H8009, CALG_SHA_256 = &H800C
    Const CRYPT_IPSEC_HMAC_KEY = &H100, HP_HASHVAL = 2, HP_HMAC_INFO = 5
    Dim prov As Long, k As Long, h As Long, kb() As Byte, blob() As Byte, mb() As Byte, r(0 To 31) As Byte
    Dim info As HMAC_INFO, n As Long, i As Long, hex As String
    kb = StrConv(chave, vbFromUnicode): mb = StrConv(msg, vbFromUnicode)
    n = UBound(kb) + 1
    ReDim blob(0 To 11 + n)
    blob(0) = 8: blob(1) = 2: blob(4) = &H2: blob(5) = &H66          ' PLAINTEXTKEYBLOB, versao 2, CALG_RC2 (&H6602)
    blob(8) = n And &HFF: blob(9) = (n \ &H100) And &HFF            ' tamanho da chave (little endian)
    For i = 0 To n - 1: blob(12 + i) = kb(i): Next
    If CryptAcquireContext(prov, vbNullString, vbNullString, PROV_RSA_AES, CRYPT_VERIFYCONTEXT) = 0 Then Exit Function
    If CryptImportKey(prov, blob(0), UBound(blob) + 1, 0, CRYPT_IPSEC_HMAC_KEY, k) <> 0 Then
        If CryptCreateHash(prov, CALG_HMAC, k, 0, h) <> 0 Then
            info.HashAlgid = CALG_SHA_256
            If CryptSetHashParam(h, HP_HMAC_INFO, info, 0) <> 0 Then
                If CryptHashData(h, mb(0), UBound(mb) + 1, 0) <> 0 Then
                    n = 32
                    If CryptGetHashParam(h, HP_HASHVAL, r(0), n, 0) <> 0 Then
                        For i = 0 To 31: hex = hex & LCase$(Right$("0" & Hex$(r(i)), 2)): Next
                        HmacSha256Hex = hex
                    End If
                End If
            End If
            CryptDestroyHash h
        End If
        CryptDestroyKey k
    End If
    CryptReleaseContext prov, 0
End Function
