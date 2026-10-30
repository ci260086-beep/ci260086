using System.Collections.Generic;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>Name to sprite lookup, filled by the editor setup step.</summary>
    public sealed class ArtLibrary : ScriptableObject
    {
        private const float PixelsPerUnit = 16f;

        [SerializeField] private List<Sprite> sprites = new List<Sprite>();

        private Dictionary<string, Sprite> byName;

        public void SetSprites(List<Sprite> list) => sprites = list;

        public Sprite Get(string name)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, Sprite>();
                foreach (var s in sprites) byName[s.name] = s;
            }
            return byName[name];
        }

        /// <summary>Cuts a horizontal strip into equal frames.</summary>
        public Sprite[] Strip(string name, int frameWidth)
        {
            var sheet = Get(name).texture;
            int count = sheet.width / frameWidth;
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = Sprite.Create(sheet, new Rect(i * frameWidth, 0, frameWidth, sheet.height),
                    new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            return frames;
        }
    }
}
