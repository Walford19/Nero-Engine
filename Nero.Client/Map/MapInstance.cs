using Nero.Client.Player;
using Nero.Client.Scenes.GameplayComponents;
using Nero.Client.World;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Nero.Client.Map
{
    using static Renderer;
    class MapInstance
    {
        #region Static        
        public static MapInstance Current = null;

        /// <summary>
        /// Cria o mapa
        /// </summary>
        /// <returns></returns>
        public static MapInstance Create()
        {
            var m = new MapInstance();
            for (int i = 0; i < (int)Layers.count; i++)
                m.Layer[i].SetMap(m);

            return m;
        }

        /// <summary>
        /// Salva o mapa
        /// </summary>
        public static void Save()
        {
            var path = Environment.CurrentDirectory + "/data/map/";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var filePath = path + $"{Character.My.MapID}.map";
            var json = JsonConvert.SerializeObject(Current);
            File.WriteAllBytes(filePath, MemoryService.Compress(Encoding.UTF8.GetBytes(json)));
        }

        /// <summary>
        /// Carrega o mapa
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public static MapInstance Load(int ID)
        {
            var path = Environment.CurrentDirectory + "/data/map/";
            var filePath = path + $"{Character.My.MapID}.map";

            if (!File.Exists(filePath))
                return Create();

            var data = MemoryService.Decompress(File.ReadAllBytes(filePath));
            var json = Encoding.UTF8.GetString(data);
            var m = JsonConvert.DeserializeObject<MapInstance>(json);
            for (int i = 0; i < (int)Layers.count; i++)
            {
                m.Layer[i].SetMap(m, false);
                for (int x = 0; x <= m.Size.x; x++)
                    for (int y = 0; y <= m.Size.y; y++)
                        m.Layer[i].chunks[x, y]?.SetLayer(m.Layer[i]);
            }

            return m;
        }

        #endregion

        public int Revision = 0;                    // Revis�o do mapa
        public string Name = "";                    // Nome do mapa
        public Int2 Size = new Int2(59, 31);        // Tamanho do mapa
        public Layer[] Layer;                       // Camadas
        public List<AttributeInfo>[,] Attributes;   // Atributos
        public ZoneTypes Zone = ZoneTypes.Normal;   // Zona de mapa
        public string MusicName = "None";           // Nome da Musica
        public int FogSpeed = 80;                   // Velocidade do fog
        public byte FogOpacity = 20;                // Opacidade do fog
        public int FogID = 0;                       // ID do gr�fico do fog
        public bool FogBlend = false;               // Blend Add no Fog
        public int PanoramaID = 0;                  // Gr�fico do panorama
        public int[] Warps = new int[4];            // Teleportes

        // Client Only
        [JsonIgnore]
        public int offWater { get; private set; }   // Anima��o de frame para �gua        
        bool Animation = false;                     // Anima��o de camada        
        long timerAnimation;                        // Tempo para anima��o
        float offFog;                               // Fog move

        /// <summary>
        /// Construtor
        /// </summary>
        private MapInstance()
        {
            Layer = new Layer[(int)Layers.count];
            for (int i = 0; i < Layer.Length; i++)
                Layer[i] = new Layer();

            Attributes = new List<AttributeInfo>[Size.x + 1, Size.y + 1];
            for (int x = 0; x <= Size.x; x++)
                for (int y = 0; y <= Size.y; y++)
                    Attributes[x, y] = new List<AttributeInfo>();
        }

        /// <summary>
        /// Desenha o panorama
        /// </summary>
        /// <param name="target"></param>
        public void DrawPanorama(RenderTarget target)
        {
            if (PanoramaID == 0)
                return;

            var tex = GlobalResources.Panorama[PanoramaID];
            DrawTexture(target, tex, new Rectangle(Vector2.Zero, Game.Size));
        }

        /// <summary>
        /// Desenha o ch�o
        /// </summary>
        /// <param name="target"></param>
        public void DrawGround(RenderTarget target)
        {
            for (Layers i = Layers.Ground; i <= Layers.Mask2Anim; i++)
                if (i == Layers.MaskAnim || i == Layers.Mask2Anim)
                {
                    if (Animation)
                        Layer[(int)i].Draw(target);
                }
                else
                    Layer[(int)i].Draw(target);
        }

        /// <summary>
        /// Desenha os sobrepostos
        /// </summary>
        /// <param name="target"></param>
        public void DrawFringe(RenderTarget target)
        {
            for (Layers i = Layers.Fringe; i <= Layers.Fringe2Anim; i++)
                if (i == Layers.FringeAnim || i == Layers.Fringe2Anim)
                {
                    if (Animation)
                        Layer[(int)i].Draw(target);
                }
                else
                    Layer[(int)i].Draw(target);
        }

        /// <summary>
        /// Desenha o fog
        /// </summary>
        /// <param name="target"></param>
        public void DrawFog(RenderTarget target)
        {
            if (FogID == 0)
                return;

            var tex = GlobalResources.Fog[FogID];
            var countX = (int)(Game.Size.x / 256) + 1;
            var countY = (int)(Game.Size.y / 256) + 1;

            for (int x = 0; x <= countX; x++)
                for (int y = 0; y <= countY; y++)
                    DrawTexture(target, tex, new Rectangle(new Vector2(x, y) * 256 - new Vector2(offFog), new Vector2(256)),
                        new Rectangle(Vector2.Zero, tex.size), new Color(255, 255, 255, FogOpacity), Vector2.Zero, 0,
                        FogBlend ? new RenderStates(BlendMode.Add) : RenderStates.Default);

            offFog += FogSpeed * Game.DeltaTime;
            if (offFog > 256) offFog = 0;
        }

        /// <summary>
        /// Desenha os atributos :: EDITOR ONLY
        /// </summary>
        /// <param name="target"></param>
        public void DrawAttributes(RenderTarget target)
        {
            var ed = Game.GetScene().FindControl<frmEditor_Map>();
            var start = Camera.Start();
            var end = Camera.End(this);

            for (int x = start.x; x <= end.x; x++)
                for (int y = start.y; y <= end.y; y++)
                {
                    if (Attributes[x, y].Count > 0 && Attributes[x, y].Any(i => i.Type == ed.CurrentAttribute))
                    {
                        var text = "B";
                        var c = Color.Red;
                        switch (ed.CurrentAttribute)
                        {
                            case AttributeTypes.Warp:
                                text = "W";
                                c = Color.Blue;
                                break;
                        }

                        DrawText(target, text, 14, new Vector2(x, y) * 32 + new Vector2((32 - GetTextWidth(text, 14)) / 2, 2), c, 1, new Color(30, 30, 30));
                    }
                }
        }


        /// <summary>
        /// Atualiza o mapa
        /// </summary>
        public void Update()
        {
            if (Environment.TickCount64 > timerAnimation)
            {
                Animation = !Animation;
                offWater++;
                if (offWater > 2)
                    offWater = 0;
                timerAnimation = Environment.TickCount64 + 250;
            }
        }

        /// <summary>
        /// Adiciona um novo chunk
        /// </summary>
        /// <param name="currentlayer"></param>
        /// <param name="tileid"></param>
        /// <param name="rect"></param>
        /// <param name="Position"></param>
        public void AddChunk(int currentlayer, ChunkTypes type, int tileid, Vector2 Source, Vector2 Position)
        {
            if (currentlayer < 0 || currentlayer >= Layer.Length)
                return;

            if (tileid < 0 || tileid >= GlobalResources.Tileset.Count)
                return;

            if (Position.x < 0 || Position.x > Size.x) return;
            if (Position.y < 0 || Position.y > Size.y) return;

            var l = Layer[currentlayer];
            l.chunks[(int)Position.x, (int)Position.y] = new Chunk();
            l.chunks[(int)Position.x, (int)Position.y].SetLayer(l);
            l.chunks[(int)Position.x, (int)Position.y].type = type;
            l.chunks[(int)Position.x, (int)Position.y].Position = Position;
            l.chunks[(int)Position.x, (int)Position.y].TileID = tileid;
            l.chunks[(int)Position.x, (int)Position.y].Source = Source;
            l.chunks[(int)Position.x, (int)Position.y].VerifyAutotile();

            Vector2[] chk_pos = { Position + new Vector2(-1,-1), Position + new Vector2(0, -1), Position + new Vector2(1, -1),
            Position + new Vector2(-1,0), Position + new Vector2(1,0),
            Position + new Vector2(-1,1), Position + new Vector2(0,1), Position + new Vector2(1,1)};

            foreach (var i in chk_pos)
                if (i.x >= 0 && i.x <= Size.x && i.y >= 0 && i.y <= Size.y)
                    l.chunks[(int)i.x, (int)i.y]?.VerifyAutotile();

        }

        /// <summary>
        /// Remove um chunk
        /// </summary>
        /// <param name="currentLayer"></param>
        /// <param name="Position"></param>
        public void RemoveChunk(int currentlayer, Vector2 Position)
        {
            if (currentlayer < 0 || currentlayer >= Layer.Length)
                return;

            if (Position.x < 0 || Position.x > Size.x) return;
            if (Position.y < 0 || Position.y > Size.y) return;
            var l = Layer[currentlayer];

            l.chunks[(int)Position.x, (int)Position.y] = null;

            Vector2[] chk_pos = { Position + new Vector2(-1,-1), Position + new Vector2(0, -1), Position + new Vector2(1, -1),
            Position + new Vector2(-1,0), Position + new Vector2(1,0),
            Position + new Vector2(-1,1), Position + new Vector2(0,1), Position + new Vector2(1,1)};

            foreach (var i in chk_pos)
                if (i.x >= 0 && i.x <= Size.x && i.y >= 0 && i.y <= Size.y)
                    l.chunks[(int)i.x, (int)i.y]?.VerifyAutotile();
        }

        /// <summary>
        /// Adiciona um atributo
        /// </summary>
        /// <param name="position"></param>
        /// <param name="type"></param>
        /// <param name="args"></param>
        public void AddAttribute(Vector2 position, AttributeTypes type, string[] args)
        {
            if (position.x < 0 || position.x > Size.x) return;
            if (position.y < 0 || position.y > Size.y) return;

            var pos = position.ToInt2();
            if (!Attributes[pos.x, pos.y].Any(i => i.Type == type))
                Attributes[pos.x, pos.y].Add(new AttributeInfo(type, args));
            else
            {
                var find = Attributes[pos.x, pos.y].FindLast(i => i.Type == type);
                find.Type = type;
                find.args = args;
            }
        }

        /// <summary>
        /// Remove um atributo
        /// </summary>
        /// <param name="position"></param>
        /// <param name="type"></param>
        public void RemoveAttribute(Vector2 position, AttributeTypes type)
        {
            if (position.x < 0 || position.x > Size.x) return;
            if (position.y < 0 || position.y > Size.y) return;

            var pos = position.ToInt2();
            var find = Attributes[pos.x, pos.y].FindLast(i => i.Type == type);
            if (find != null)
                Attributes[pos.x, pos.y].Remove(find);
        }

        /// <summary>
        /// Altera o tamanho do mapa
        /// </summary>
        /// <param name="valueX"></param>
        /// <param name="valueY"></param>
        public void SetSize(int valueX, int valueY)
        {
            foreach (var i in Layer)
                i.SetSize(valueX, valueY);

            var copyAttr = new List<AttributeInfo>[valueX + 1, valueY + 1];
            for (int x = 0; x <= valueX; x++)
                for (int y = 0; y <= valueY; y++)
                    copyAttr[x, y] = new List<AttributeInfo>();

            int copyX = Math.Min(valueX, Size.x);
            int copyY = Math.Min(valueY, Size.y);
            for (int x = 0; x <= copyX; x++)
                for (int y = 0; y <= copyY; y++)
                    copyAttr[x, y] = Attributes[x, y];

            Attributes = copyAttr;

            Size = new Int2(valueX, valueY);
        }
    }
}
