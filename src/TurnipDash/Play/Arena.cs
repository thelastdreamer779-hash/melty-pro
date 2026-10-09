using UnityEngine;

namespace TurnipDash.Play
{
    /// A space far above the current scene with its own camera drawn over Hollow Knight's.
    public sealed class Arena
    {
        public static readonly Vector3 Origin = new Vector3(0f, 20000f, 0f);
        public readonly GameObject Root;
        public readonly Camera Cam;
        static Sprite white;

        public Arena(string name, Color background, float viewHeight)
        {
            Root = new GameObject("TurnipDash." + name);
            Root.transform.position = Origin;
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(Root.transform, false);
            camGo.transform.localPosition = new Vector3(0, 0, -50);
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = viewHeight / 2f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = background;
            Cam.depth = 50;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 200f;
        }

        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                    t.SetPixels32(px); t.Apply();
                    white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                }
                return white;
            }
        }

        /// A sprite scaled so its longer side is `size` world units (Sprites from ContentStore are 1 unit).
        public SpriteRenderer Add(string name, Sprite s, Vector2 local, float size, Color tint, int order, float rotation = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = new Vector3(local.x, local.y, 0);
            go.transform.localRotation = Quaternion.Euler(0, 0, rotation);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s ?? White;
            r.color = tint;
            r.sortingOrder = order;
            var b = r.sprite.bounds.size;
            var k = size / Mathf.Max(0.0001f, Mathf.Max(b.x, b.y));
            go.transform.localScale = new Vector3(k, k, 1);
            return r;
        }

        public void Look(Vector2 local) => Cam.transform.localPosition = new Vector3(local.x, local.y, -50);

        public void Destroy() => Object.Destroy(Root);
    }
}
