using System.IO;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Vanilla texture dump: launch with -quicktest -rhtexdump. Blits the humanlike body and
    /// head textures into readable copies and writes PNGs next to the mod (tools/vanilla_dump/)
    /// so vestment art can be drawn against exact silhouettes. Then exits.
    /// </summary>
    public class GameComponent_TexDumpSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhtexdump");

        private int state;
        private float nextStateTime = -1f;

        private static readonly string[] Paths =
        {
            "Things/Pawn/Humanlike/Bodies/Naked_Male_south",
            "Things/Pawn/Humanlike/Bodies/Naked_Male_east",
            "Things/Pawn/Humanlike/Bodies/Naked_Male_north",
            "Things/Pawn/Humanlike/Bodies/Naked_Female_south",
            "Things/Pawn/Humanlike/Bodies/Naked_Female_east",
            "Things/Pawn/Humanlike/Bodies/Naked_Female_north",
            "Things/Pawn/Humanlike/Heads/Male/Male_Average_Normal_south",
            "Things/Pawn/Humanlike/Heads/Male/Male_Average_Normal_east",
            "Things/Pawn/Humanlike/Heads/Male/Male_Average_Normal_north",
            "Things/Pawn/Humanlike/Heads/Female/Female_Average_Normal_south",
            "Things/Pawn/Humanlike/Heads/Female/Female_Average_Normal_east",
            "Things/Pawn/Humanlike/Heads/Female/Female_Average_Normal_north",
            "Things/Pawn/Humanlike/Apparel/Hood/Hood_south",
            "Things/Pawn/Humanlike/Apparel/Hood/Hood_east",
            "Things/Pawn/Humanlike/Apparel/Hood/Hood_north",
            "Things/Item/Equipment/WeaponMelee/BreachAxe",
            "Things/Item/Equipment/WeaponMelee/LongSword",
            "Things/Item/Equipment/WeaponMelee/Mace",
            "Things/Building/Production/TableSmithingFueled_south",
            "Things/Building/Production/TableSmithingFueled_east",
            "Things/Building/Production/TableMachining_south",
        };

        public GameComponent_TexDumpSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 1)
            {
                return;
            }
            if (Find.CurrentMap == null)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f)
            {
                nextStateTime = now + 3f;
                return;
            }
            if (now < nextStateTime)
            {
                return;
            }

            string dir = Path.Combine(GenFilePaths.ModsFolderPath, "RimHeroes", "tools", "vanilla_dump");
            Directory.CreateDirectory(dir);
            int ok = 0;
            foreach (var path in Paths)
            {
                try
                {
                    var tex = ContentFinder<Texture2D>.Get(path, false);
                    if (tex == null)
                    {
                        Log.Warning($"[RimHeroes.TexDump] missing: {path}");
                        continue;
                    }
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    readable.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    string file = Path.Combine(dir, path.Replace('/', '_') + ".png");
                    File.WriteAllBytes(file, readable.EncodeToPNG());
                    Object.Destroy(readable);
                    ok++;
                }
                catch (System.Exception e)
                {
                    Log.Error($"[RimHeroes.TexDump] {path} failed: {e}");
                }
            }
            Log.Message($"[RimHeroes.TexDump] RESULT: dumped {ok}/{Paths.Length} verdict={(ok > 0 ? "PASS" : "FAIL")}");
            state = 2;
            Root.Shutdown();
        }
    }
}
