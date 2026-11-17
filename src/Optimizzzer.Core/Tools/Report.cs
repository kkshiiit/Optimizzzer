using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;

namespace Optimizzzer.Core.Tools
{
    /// <summary>A self-contained HTML report (no scripts, no external files) of what was changed.</summary>
    public static class HtmlReport
    {
        public static string Build(Lang lang, ISystemInfo info, JournalSession session, IList<Tweak> catalog,
                                   Measurement before, Measurement after, DateTime now)
        {
            string T(string es, string en) => lang == Lang.En ? en : es;
            string H(string s) => WebUtility.HtmlEncode(s ?? "");
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"").Append(lang == Lang.En ? "en" : "es").Append("\"><head><meta charset=\"utf-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.Append("<title>").Append(T("Informe de Optimizzzer", "Optimizzzer report")).Append("</title><style>");
            sb.Append(":root{--bg:#F7F7F8;--fg:#1B1B1F;--muted:#5A5A63;--line:#E2E2E6;--ok:#1E7B34;--mod:#8A5300;--adv:#B3261E}");
            sb.Append("@media(prefers-color-scheme:dark){:root{--bg:#1C1C1F;--fg:#F2F2F4;--muted:#A8A8B0;--line:#3A3A40;--ok:#6CCB7F;--mod:#F2B24B;--adv:#FF8A80}}");
            sb.Append("body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.5 'Segoe UI',system-ui,sans-serif}main{max-width:880px;margin:0 auto;padding:24px 16px}");
            sb.Append("h1{font-size:28px;font-weight:600;margin:0 0 4px}h2{font-size:20px;font-weight:600;margin:32px 0 8px}p{margin:0 0 8px}.muted{color:var(--muted)}");
            sb.Append("table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:8px;border-bottom:1px solid var(--line);vertical-align:top}th{font-weight:600}");
            sb.Append(".ok{color:var(--ok)}.mod{color:var(--mod)}.adv{color:var(--adv)}code{font-family:Consolas,monospace;font-size:12px;word-break:break-all}");
            sb.Append(".wrap{overflow-x:auto}</style></head><body><main>");

            sb.Append("<header><h1>").Append(T("Informe de Optimizzzer", "Optimizzzer report")).Append("</h1>");
            sb.Append("<p class=\"muted\">").Append(H(now.ToString("f", lang == Lang.En ? new CultureInfo("en-US") : new CultureInfo("es-ES")))).Append("</p></header>");

            if (info != null)
            {
                sb.Append("<section><h2>").Append(T("Equipo", "PC")).Append("</h2><div class=\"wrap\"><table>");
                Row(sb, T("Sistema", "System"), info.ProductName + " " + info.DisplayVersion + " (" + info.Build + "." + info.Ubr + ")");
                Row(sb, "CPU", info.CpuName + " · " + info.LogicalProcessors + " " + T("hilos", "threads"));
                Row(sb, "RAM", info.TotalMemoryMb.ToString("N0", CultureInfo.InvariantCulture) + " MB");
                Row(sb, "GPU", string.Join(", ", info.Gpus));
                Row(sb, T("Tipo", "Type"), info.IsLaptop ? T("Portátil", "Laptop") : T("Sobremesa", "Desktop"));
                sb.Append("</table></div></section>");
            }

            if (session != null)
            {
                sb.Append("<section><h2>").Append(T("Cambios", "Changes")).Append("</h2>");
                sb.Append("<p>").Append(session.RestorePointCreated
                    ? "<span class=\"ok\">" + T("Punto de restauración creado antes de aplicar.", "Restore point created before applying.") + "</span>"
                    : "<span class=\"adv\">" + T("No se pudo crear el punto de restauración: ", "Could not create a restore point: ") + H(session.RestorePointError) + "</span>").Append("</p>");
                sb.Append("<div class=\"wrap\"><table><thead><tr><th>").Append(T("Ajuste", "Tweak")).Append("</th><th>").Append(T("Riesgo", "Risk"))
                  .Append("</th><th>").Append(T("Resultado", "Result")).Append("</th><th>").Append(T("Qué se cambió", "What changed")).Append("</th></tr></thead><tbody>");
                foreach (var e in session.Entries)
                {
                    var t = catalog.FirstOrDefault(x => x.Id == e.TweakId);
                    var title = t?.Title[lang] ?? e.TweakId;
                    var risk = t == null ? "" : RiskLabel(t.Risk, lang);
                    var riskClass = t == null ? "" : t.Risk == Risk.Safe ? "ok" : t.Risk == Risk.Moderate ? "mod" : "adv";
                    string result;
                    switch (e.ResultKind)
                    {
                        case EntryResult.Applied: result = "<span class=\"ok\">" + T("Aplicado", "Applied") + "</span>"; break;
                        case EntryResult.Skipped: result = "<span class=\"muted\">" + T("Ya estaba aplicado", "Already applied") + "</span>"; break;
                        default: result = "<span class=\"adv\">" + T("Error, revertido: ", "Failed, rolled back: ") + H(e.Error) + "</span>"; break;
                    }
                    var details = string.Join("<br>", e.Actions.Select(a => "<code>" + H(t != null && a.Index < t.Actions.Count ? t.Actions[a.Index].Describe(lang) : a.Description) + "</code>"));
                    sb.Append("<tr><td>").Append(H(title)).Append("</td><td class=\"").Append(riskClass).Append("\">").Append(H(risk))
                      .Append("</td><td>").Append(result).Append("</td><td>").Append(details).Append("</td></tr>");
                }
                sb.Append("</tbody></table></div></section>");
            }

            if (before != null || after != null)
            {
                sb.Append("<section><h2>").Append(T("Mediciones", "Measurements")).Append("</h2>");
                sb.Append("<p class=\"muted\">").Append(T("Valores leídos de este equipo. \"Sin dato\" significa que no se pudo medir; la duración del arranque solo cambia tras reiniciar.",
                    "Values read from this PC. \"No data\" means it could not be measured; boot duration only changes after a restart.")).Append("</p>");
                sb.Append("<div class=\"wrap\"><table><thead><tr><th>").Append(T("Métrica", "Metric")).Append("</th><th>").Append(T("Antes", "Before"))
                  .Append("</th><th>").Append(T("Después", "After")).Append("</th></tr></thead><tbody>");
                foreach (var d in MeasurementCompare.Compare(before, after))
                    sb.Append("<tr><td>").Append(H(d.Name[lang])).Append("</td><td>").Append(Num(d.Before, d.Unit, lang)).Append("</td><td>").Append(Num(d.After, d.Unit, lang)).Append("</td></tr>");
                sb.Append("</tbody></table></div></section>");
            }

            sb.Append("<footer><p class=\"muted\">").Append(T("Puedes deshacer cada cambio desde Optimizzzer > Historial.", "You can undo each change from Optimizzzer > History.")).Append("</p></footer>");
            sb.Append("</main></body></html>");
            return sb.ToString();
        }

        public static string RiskLabel(Risk r, Lang lang)
        {
            switch (r)
            {
                case Risk.Safe: return lang == Lang.En ? "Safe" : "Seguro";
                case Risk.Moderate: return lang == Lang.En ? "Moderate" : "Moderado";
                default: return lang == Lang.En ? "Advanced" : "Avanzado";
            }
        }

        private static string Num(double? v, string unit, Lang lang)
        {
            if (v == null) return lang == Lang.En ? "No data" : "Sin dato";
            var culture = lang == Lang.En ? new CultureInfo("en-US") : new CultureInfo("es-ES");
            var fmt = unit == "s" ? "N1" : "N0";
            return v.Value.ToString(fmt, culture) + (unit.Length > 0 ? " " + unit : "");
        }

        private static void Row(StringBuilder sb, string k, string v) =>
            sb.Append("<tr><th scope=\"row\">").Append(WebUtility.HtmlEncode(k)).Append("</th><td>").Append(WebUtility.HtmlEncode(v ?? "")).Append("</td></tr>");
    }
}
