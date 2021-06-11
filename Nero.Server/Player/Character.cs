using LiteNetLib;
using Nero.Server.Map;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nero.Server.Player
{
    class Character : Entity
    {
        #region 
        public static List<Character> Items = new List<Character>();
        public static readonly string Path = Environment.CurrentDirectory + "/data/character/";
        

        /// <summary>
        /// Verifica a existência do diretório
        /// </summary>
        public static void CheckDirectory()
        {
            Console.WriteLine("Checando diretório de personagens...");
            if (!Directory.Exists(Path))
                Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Verifica se existe o personagem
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool Exist(string name)
            => File.Exists(Path + $"{name.ToLower()}.json");

        /// <summary>
        /// Salva o personagem
        /// </summary>
        /// <param name="controller"></param>
        public static void Save(Character controller)
        {
            var filePath = Path + $"{controller.Name.ToLower()}.json";
            JsonHelper.Save(filePath, controller);
        }

        /// <summary>
        /// Carrega o personagem
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Character Load(string name)
        {
            var filePath = Path + $"{name.ToLower()}.json";

            if (!File.Exists(filePath))
                return null;
            
            Character c;
            JsonHelper.Load(filePath, out c);
            return c;
        }

        /// <summary>
        /// Procura o personagem com o nome desejado
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Character Find(string name)
            => Items.Find(i => i.Name.ToLower().Equals(name.ToLower()));

        /// <summary>
        /// Procura o personagem com a entrada de conexão
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public static Character Find(NetPeer peer)
            => Items.Find(i => i.peer == peer);

        /// <summary>
        /// Procura o personagem com a conta
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public static Character Find(Account account)
            => Items.Find(i => i.account == account);

        #endregion 


        // Publics
        public string Name = "";                                        // Nome
        public int ClassID = 0;                                         // Id da classe
        public int SpriteID = 0;                                        // Aparência
        public int Level = 1;                                           // Nível do personagem
        public long Experience = 0;                                     // Experiência
        public int[] StatPrimary = new int[(int)StatPrimaries.count];   // Atributos primários
        public Directions Direction = Directions.Down;                  // Direção do personagem
        public int Points = 0;                                          // Pontos de atributos        
        public AccessLevels AccessLevel = AccessLevels.Player;          // Acesso de administrador


        [JsonIgnore]
        public NetPeer peer = null;     // Entrada de conexão
        [JsonIgnore]
        public Account account = null;  // Conta vinculada


        /// <summary>
        /// Construtor
        /// </summary>
        public Character()
        {
        }

        /// <summary>
        /// Instancia no qual o jogador está
        /// </summary>
        /// <returns></returns>
        public IInstance GetInstance()
            => MapInstance.Items[MapID];

        /// <summary>
        /// Atributos vitals máximo
        /// </summary>
        /// <param name="vital"></param>
        /// <returns></returns>
        public override int VitalMaximum(Vitals vital)
        {
            if (vital == Vitals.HP)
            {
                int value = 150 + 50 * Level;
                value += StatPrimary[(int)StatPrimaries.Constitution] * 5; 
                return value;
            }
            else if(vital == Vitals.MP)
            {
                int value = 50 + 20 * Level;
                value += StatPrimary[(int)StatPrimaries.Mental] * 2;
                return value;
            }

            return 0;
        }
    }
}