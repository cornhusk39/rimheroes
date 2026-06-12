using System.Text;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Mim naming: every mim gets a name drawn from the pool.
    /// 70% one name, 29% two names, 0.9% three names, 0.1% four names ("Pim Moo Nimpo").
    /// </summary>
    public static class MimNames
    {
        private static readonly string[] Pool =
        {
            "Bim", "Pim", "Nim", "Wim", "Lim", "Fim", "Mo", "Bo", "Po", "Nu", "Mu", "Bu", "Lu",
            "Wob", "Bib", "Pob", "Nib", "Mub", "Dob", "Tum", "Pum", "Num", "Mam", "Bem", "Pep",
            "Nin", "Min", "Bun", "Pip", "Dot", "Wee", "Moo", "Boop", "Bobo", "Momo", "Pipo",
            "Nono", "Wumbo", "Bimble", "Pimble", "Womble", "Mumble", "Nimble", "Bobbin", "Mippet",
            "Poppet", "Wobbet", "Nubbin", "Bumbo", "Pombo", "Mimble", "Fimble", "Doodle", "Noodle",
            "Pudding", "Bibble", "Pobble", "Wibble", "Mopsy", "Flopsy", "Bimsy", "Mimsy", "Popsy",
            "Wumple", "Bumple", "Mumpo", "Nimpo", "Pimpo", "Lulu", "Mimi", "Bibi", "Popo", "Nunu",
            "Wuwu", "Lolo", "Fifi", "Bubu", "Tutu", "Dodo", "Pippin", "Muffin", "Boffin", "Wiffle",
            "Piffle", "Toto", "Bonbon", "Pompom", "Tumtum", "Bumbum"
        };

        public static string Generate()
        {
            float roll = Rand.Value;
            int parts = roll < 0.70f ? 1 : roll < 0.99f ? 2 : roll < 0.999f ? 3 : 4;
            var sb = new StringBuilder();
            for (int i = 0; i < parts; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(Pool.RandomElement());
            }
            return sb.ToString();
        }

        public static void EnsureNamed(Pawn mim)
        {
            if (mim.Name == null)
            {
                mim.Name = new NameSingle(Generate());
            }
        }
    }
}
