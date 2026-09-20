using System;
using System.Collections.Generic;
using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    public class TrainCarCardSpriteLibrary : MonoBehaviour
    {
        [Serializable]
        public class ColorSpritePair
        {
            public TrainCarColor color;
            public Sprite sprite;
        }

        [SerializeField] private List<ColorSpritePair> spriteMap = new List<ColorSpritePair>();

        private readonly Dictionary<TrainCarColor, Sprite> _spriteLookup = new Dictionary<TrainCarColor, Sprite>();

        private void Awake()
        {
            RebuildLookup();
        }

        public void RebuildLookup()
        {
            _spriteLookup.Clear();

            if (spriteMap == null)
            {
                return;
            }

            foreach (var pair in spriteMap)
            {
                if (pair.sprite == null)
                {
                    continue;
                }

                _spriteLookup[pair.color] = pair.sprite;
            }
        }

        public Sprite GetSprite(TrainCarColor color)
        {
            if (_spriteLookup.TryGetValue(color, out var sprite) && sprite != null)
            {
                return sprite;
            }

            Debug.LogWarning($"[TrainCarCardSpriteLibrary] 没有为颜色 {color} 配置对应切片 Sprite。");
            return null;
        }
    }
}
