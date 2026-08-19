using Obrigenie.Models;

namespace Obrigenie.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Calcul de la disposition des notes dans une grille horaire.
    //
    // Partagé par la grille HTML de la vue jour (Index.razor) et par l'export
    // PDF, pour que le papier montre exactement la même fusion de cellules que
    // l'écran : une note occupe un seul bloc couvrant toutes ses heures, et deux
    // notes qui se chevauchent partagent ce bloc.
    // ─────────────────────────────────────────────────────────────────────────
    public static class NoteLayout
    {
        // Une plage d'heures rendue comme un seul bloc fusionné.
        // Start est inclusif, End exclusif (une note 09:00→11:00 donne Start=9, End=11).
        public sealed class Bloc
        {
            public int Start;
            public int End;
            public List<Note> Notes = new();
        }

        // Première heure NON couverte par la note, c.-à-d. le nombre de lignes qu'elle
        // occupe dans la grille. Une fin à 11:00 s'arrête à la ligne 10:00 ; une fin à
        // 11:15 déborde sur la ligne 11:00 et l'occupe donc entièrement. Minimum : une ligne.
        public static int RowEnd(Note n)
        {
            int endH = n.EndHour > 0 ? n.EndHour : n.Hour + 1;
            int endM = n.EndHour > 0 ? n.EndMinute : 0;
            if (endM > 0) endH++;
            return Math.Max(n.Hour + 1, endH);
        }

        // Regroupe les notes en blocs à fusionner dans la grille.
        // Les notes qui se chevauchent partagent le même bloc : sans cela, la note
        // commençant à l'intérieur d'une autre tomberait sur une ligne déjà absorbée
        // et ne serait pas rendue. Les notes débordant de la grille sont rognées sur
        // [heureDebut, heureFin[ ; celles entièrement hors grille sont ignorées.
        public static List<Bloc> Blocs(IEnumerable<Note> notes, int heureDebut, int heureFin)
        {
            var blocs = new List<Bloc>();

            foreach (var n in notes.OrderBy(n => n.Hour).ThenBy(n => n.Minute))
            {
                int start = Math.Max(n.Hour, heureDebut);
                int end   = Math.Min(RowEnd(n), heureFin);
                if (end <= start) continue;

                // Chevauchement avec le bloc précédent : on l'étend au lieu d'en créer un nouveau
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

        // Extrait le nom du cours de la ligne "Cours : ..." du contexte de cascade figé
        // sur la note. Chaîne vide quand la note n'est rattachée à aucune cascade.
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

        // Plage horaire d'une note au format "09:00 -> 11:00".
        public static string PlageHoraire(Note n)
        {
            int endH = n.EndHour > 0 ? n.EndHour : n.Hour + 1;
            int endM = n.EndHour > 0 ? n.EndMinute : 0;
            return $"{n.Hour:D2}:{n.Minute:D2} -> {endH:D2}:{endM:D2}";
        }
    }
}
