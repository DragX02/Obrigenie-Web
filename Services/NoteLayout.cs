using Obrigenie.Models;

namespace Obrigenie.Services
{
    public static class NoteLayout
    {
        public sealed class Bloc
        {
            public int Start;
            public int End;
            public List<Note> Notes = new();
        }

        public static int RowEnd(Note n)
        {
            int endH = n.EndHour > 0 ? n.EndHour : n.Hour + 1;
            int endM = n.EndHour > 0 ? n.EndMinute : 0;
            if (endM > 0) endH++;
            return Math.Max(n.Hour + 1, endH);
        }

        public static List<Bloc> Blocs(IEnumerable<Note> notes, int heureDebut, int heureFin)
        {
            var blocs = new List<Bloc>();

            foreach (var n in notes.OrderBy(n => n.Hour).ThenBy(n => n.Minute))
            {
                int start = Math.Max(n.Hour, heureDebut);
                int end   = Math.Min(RowEnd(n), heureFin);
                if (end <= start) continue;

                if (blocs.Count > 0 && start < blocs[^1].End)
                {
                    blocs[^1].End = Math.Max(blocs[^1].End, end);
                    blocs[^1].Notes.Add(n);
                }
                else
                {
                    blocs.Add(new Bloc { Start = start, End = end, Notes = { n } });
                }
            }

            return blocs;
        }

        public static string CourseLabel(Note n)
        {
            if (string.IsNullOrEmpty(n.ViseeContexte)) return string.Empty;

            foreach (var ligne in n.ViseeContexte.Split('\n'))
            {
                var t = ligne.Trim();
                if (t.StartsWith("Cours :", StringComparison.Ordinal))
                    return t["Cours :".Length..].Trim();
            }

            return string.Empty;
        }

        public static string PlageHoraire(Note n)
        {
            int endH = n.EndHour > 0 ? n.EndHour : n.Hour + 1;
            int endM = n.EndHour > 0 ? n.EndMinute : 0;
            return $"{n.Hour:D2}:{n.Minute:D2} -> {endH:D2}:{endM:D2}";
        }
    }
}
