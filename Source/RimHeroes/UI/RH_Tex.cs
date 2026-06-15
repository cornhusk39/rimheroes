using UnityEngine;
using Verse;

namespace RimHeroes
{
    [StaticConstructorOnStartup]
    public static class RH_Tex
    {
        public static readonly Texture2D LongRest = ContentFinder<Texture2D>.Get("UI/RimHeroes/LongRest");
        public static readonly Texture2D RevertForm = ContentFinder<Texture2D>.Get("UI/RimHeroes/RevertForm");
        public static readonly Texture2D D20Die = ContentFinder<Texture2D>.Get("UI/RimHeroes/D20");
    }
}
