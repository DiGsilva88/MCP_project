//os periodos que o modelo pode pedir
//lista fechada de periodos:o modelo escolhe, o servidor calcula as datas


using System.IO.Pipes;
using System.Reflection.Metadata.Ecma335;

namespace Vendas.Servidor.Modelos;

public enum Periodo
   {
      Ultimos7Dias,
      Ultimos30Dias,
      Ultimos90Dias,
      Ultimos12Meses,
      AnoAtual,
      AnoAnterior,
      MesAtual,
      MesAnterior
   }

   public static class Periodos
   {
        private static readonly string [] Meses = 
      ["janeiro","fevereiro","março","abril","maio","junho","julho", "agosto","setembro","outubro","novembro","dezembro"];
      //mes /ano concretos
       
      
//devolve o intervalo [desde,ate[ -"ate" não está incluido
//exemplo: mes anterior a 15-10-2026 - desde = 01-09-2026, ate = 01-10-2026
//recebe hoje como parametro, para poder testar com datas diferentes da data de hoje
      public static (DateOnly desde, DateOnly ate) Intervalo(Periodo periodo, DateOnly hoje)
    {
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var inicioAno = new DateOnly(hoje.Year, 1, 1);
        var amanha = hoje.AddDays(1);

        return periodo switch
        {
            Periodo.Ultimos7Dias => (hoje.AddDays(-6), amanha),
            Periodo.Ultimos30Dias => (hoje.AddDays(-29), amanha),
            Periodo.Ultimos90Dias => (hoje.AddDays(-89), amanha),
            Periodo.AnoAtual => (inicioAno, amanha),
            Periodo.MesAtual => (inicioMes, amanha),
            Periodo.AnoAnterior => (inicioAno.AddYears(-1), inicioAno),
            Periodo.MesAnterior => (inicioMes.AddMonths(-1), inicioMes),
            Periodo.Ultimos12Meses => (hoje.AddMonths(-12).AddDays(1), amanha),
            _ => throw new ArgumentOutOfRangeException(nameof(periodo))
      
        };
    }

    //mes e ano concreto
    public static (DateOnly desde, DateOnly ate, string descricao) IntervaloMes(int? mes, int? ano, DateOnly hoje)

   {
      if (mes is null)
      {
         var inicioAno = new DateOnly(ano!.Value, 1,1);
         return (inicioAno, inicioAno.AddYears(1),$"ano {ano}");
      }
      var a= ano ??(mes > hoje.Month ? hoje.Year -1 : hoje.Year);
      var desde = new DateOnly(a, mes.Value,1);
      return(desde, desde.AddMonths(1),$"{Meses[mes.Value -1]} de a {a}");
   }
      //o nome do periodo para a frase de resposta
      public static string Descricao(Periodo periodo) => periodo switch
      {
          Periodo.Ultimos7Dias => "últimos 7 dias",
          Periodo.Ultimos30Dias => "últimos 30 dias",
          Periodo.Ultimos90Dias => "últimos 90 dias",
          Periodo.Ultimos12Meses => "últimos 12 meses",
          Periodo.MesAtual => "mês atual",
          Periodo.AnoAtual => "ano atual",
          Periodo.MesAnterior => "mês anterior",
          Periodo.AnoAnterior => "ano anterior",
          _=> throw new ArgumentOutOfRangeException(nameof(periodo))
                 
      };

    
          
      }





    
   


