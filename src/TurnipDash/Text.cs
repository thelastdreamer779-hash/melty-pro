using TurnipDash.Generated;

namespace TurnipDash
{
    static class Text
    {
        public static string Get(string id, params object[] args)
        {
            foreach (var r in Sheets.Ui)
                if (r.Id == id) return args.Length == 0 ? r.Text : string.Format(r.Text, args);
            return id;
        }
    }
}
