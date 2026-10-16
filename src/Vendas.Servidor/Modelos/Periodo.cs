//os periodos que o modelo pode pedir
//lista fechada de periodos:o modelo escolhe, o servidor calcula as datas


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
//devolve o intervalo [desde,ate[ -"ate" não está incluido
//exemplo: mes anterior a 15-10-2026 - desde = 01-09-2026, ate = 01-10-2026
//recebe hoje como parametro, para poder testar com datas diferentes da data de hoje
      public static (DateOnly desde, DateOnly ate) Intervalo(Periodo periodo, DateOnly hoje)
      {
         var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
         var inicioAno = new DateOnly(hoje.Year, 1, 1);
         var amanha=hoje.AddDays(1);

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
            _=> throw new ArgumentOutOfRangeException(nameof(periodo))

         };
   
      }

      //o nome do perido para a frase de resposta
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





    
   


