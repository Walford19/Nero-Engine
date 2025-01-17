using LiteNetLib;
using LiteNetLib.Utils;
using Nero.Server.Helpers;
using Nero.Server.Map;
using Nero.Server.Player;
using Nero.Server.World;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nero.Server.Network
{
    static class Receive
    {
        enum Packets
        {
            Register, Login, CreateCharacter, UseCharacter, MapAnswer, MapSave,
            MoveCharacter, ChatSpeak, OnGame, SaveNpc, RequestSpawnFactory,
            UpdateSpawnFactory, RequestAttack, ChangeDirection,
        }

        /// <summary>
        /// Recebe e direciona os pacotes
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        public static void Handle(NetPeer peer, NetDataReader buffer)
        {
            var packet = (Packets)buffer.GetShort();

            switch(packet)
            {
                case Packets.Register: Register(peer, buffer); break;
                case Packets.Login: Login(peer, buffer); break;
                case Packets.CreateCharacter: CreateCharacter(peer, buffer); break;
                case Packets.UseCharacter: UseCharacter(peer, buffer); break;
                case Packets.MapAnswer: MapAnswer(peer, buffer); break;
                case Packets.MapSave: MapSave(peer, buffer); break;
                case Packets.MoveCharacter: MoveCharacter(peer, buffer); break;
                case Packets.ChatSpeak: ChatSpeak(peer, buffer); break;
                case Packets.OnGame: OnGame(peer, buffer); break;
                case Packets.SaveNpc: SaveNpc(peer, buffer); break;
                case Packets.RequestSpawnFactory: RequestSpawnFactory(peer, buffer); break;
                case Packets.UpdateSpawnFactory: UpdateSpawnFactory(peer, buffer); break;
                case Packets.RequestAttack: RequestAttack(peer, buffer); break;
                case Packets.ChangeDirection: ChangeDirection(peer, buffer); break;
            }
        }

        /// <summary>
        /// Altera a dire��o do personagem
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void ChangeDirection(NetPeer peer, NetDataReader buffer)
        {
            var c = Character.Find(peer);
            c.Direction = (Directions)buffer.GetByte();
            Sender.ChangeDirection(c.GetInstance(), c);
        }

        /// <summary>
        /// Requer um ataque
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void RequestAttack(NetPeer peer, NetDataReader buffer)
        {
            CombatHelper.RequestAttack(Character.Find(peer));
        }

        /// <summary>
        /// Atualiza a produ��o de spawns
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void UpdateSpawnFactory(NetPeer peer, NetDataReader buffer)
        {
            var player = Character.Find(peer);
            SpawnFactory.Factories[player.MapID].Items.Clear();
            var count = buffer.GetInt();
            if (count > 0)
                for (int i = 0; i < count; i++)
                {
                    var s = new SpawnFactoryItem();
                    s.NpcID = buffer.GetInt();
                    s.BlockMove = buffer.GetBool();
                    s.Direction = (Directions)buffer.GetByte();
                    s.UsePositionSpawn = buffer.GetBool();
                    s.Position = buffer.GetVector2();
                    SpawnFactory.Factories[player.MapID].Items.Add(s);
                }

            SpawnFactory.Save(player.MapID);
        }

        /// <summary>
        /// Requesita a produ��o de spawn
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void RequestSpawnFactory(NetPeer peer, NetDataReader buffer)
        {
            Sender.RequestSpawnFactory(peer);
        }

        /// <summary>
        /// Salva o npc
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void SaveNpc(NetPeer peer, NetDataReader buffer)
        {
            var id = buffer.GetInt();
            var json = buffer.GetString();
            Npc.Items[id] = JsonConvert.DeserializeObject<Npc>(json);
            Npc.Save(id);
            Sender.UpdateNpc(id);
        }

        /// <summary>
        /// Entrou no jogo
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void OnGame(NetPeer peer, NetDataReader buffer)
        {
            var player = Character.Find(peer);

            // Mensagens ao entrar
            Sender.ChatText(player, $"Bem vindo ao {Constants.NAME}!", Color.White);
            Sender.ChatTextToAll($"O jogador {player.Name} acabou de entrar!", Color.White);
        }

        /// <summary>
        /// Fala no chat
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void ChatSpeak(NetPeer peer, NetDataReader buffer)
        {
            var text = buffer.GetString();
            ChatHelper.ProcessSpeak(Character.Find(peer), text);
        }

        /// <summary>
        /// Movimento do personagem
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void MoveCharacter(NetPeer peer, NetDataReader buffer)
        {
            var direction = (Directions)buffer.GetByte();
            var pos = buffer.GetVector2();
            var player = Character.Find(peer);

            if (!player.Position.Equals(pos))
            {
                Sender.UpdateCharacterPosition(player);
                return;
            }

            MoveHelper.Move(player, direction);
        }

        /// <summary>
        /// Salva o mapa
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void MapSave(NetPeer peer, NetDataReader buffer)
        {
            var json = buffer.GetString();

            var player = Character.Find(peer);
            MapInstance.Items[player.MapID] = JsonConvert.DeserializeObject<MapInstance>(json);
            MapInstance.Save(player.MapID);
            Sender.MapDataBut(peer);
        }

        /// <summary>
        /// Resposta da revis�o de mapa
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void MapAnswer(NetPeer peer, NetDataReader buffer)
        {
            var result = buffer.GetBool();

            if (result)            
                Sender.MapData(peer);

            // Envia dados
            Sender.CharacterDataToInstance(Character.Find(peer));
            Sender.CharacterDataAllForMe(peer);
            Sender.PrepareSpawn(peer);
            Sender.SpawnDataAll(peer);
        }

        /// <summary>
        /// Usa um personagem
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void UseCharacter(NetPeer peer, NetDataReader buffer)
        {
            var slot = buffer.GetInt();
            var acc = Account.Find(peer);

            // Carrega o personagem
            var controller = Character.Load(acc.Characters[slot]);
            controller.peer = peer;
            controller.account = acc;
            Character.Items.Add(controller);

            PlayerHelper.Join(controller);
        }

        /// <summary>
        /// Cria um novo personagem
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void CreateCharacter(NetPeer peer, NetDataReader buffer)
        {
            var slot = buffer.GetInt();
            var name = buffer.GetString();
            var classID = buffer.GetInt();
            var spriteID = buffer.GetInt();

            // Verifica se j� est� em uso
            if (Character.Exist(name))
            {
                Sender.Alert(peer, $"O nome {name} n�o est� disponivel!", $"The name {name} is not available!");
                return;
            }

            // Cria o personagem
            var c = new Character();
            c.Name = name;
            c.ClassID = classID;
            c.MapID = CharacterClass.Items[classID].MapID;
            c.Position = CharacterClass.Items[classID].StartPosition;
            c.StatPrimary = CharacterClass.Items[classID].StatPrimary;
            c.SpriteID = spriteID;
            Character.Save(c);

            var acc = Account.Find(peer);
            acc.Characters[slot] = name;
            Account.Save(acc);

            Sender.Alert(peer, "Personagem criado com sucesso!", "Character created successfully!");
            Sender.UpdateCharacters(peer);
            Sender.ChangeToSelectCharacter(peer);
        }

        /// <summary>
        /// Entra em uma conta
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void Login(NetPeer peer, NetDataReader buffer)
        {
            var accName = buffer.GetString();
            var password = buffer.GetString();

            // Verifica se existe a conta
            if (!Account.Exist(accName))
            {
                Sender.Alert(peer, $"A conta {accName} n�o foi encontrada!", $"The account {accName} is not found!");
                return;
            }

            // Carrega a conta
            var acc = Account.Load(accName);

            // Verifica as senhas
            if (password != acc.Password)
            {
                Sender.Alert(peer, "Senha incorreta!", "Incorrect password!");
                return;
            }

            // Verifica se est� em uso            
            if (Account.Items.Any(i => i.Name.ToLower().Equals(accName.ToLower())))
            {
                var fAcc = Account.Items.Find(i => i.Name.ToLower().Equals(accName.ToLower()));
                Sender.Alert(peer, "A conta j� est� em uso, reporte caso n�o seja voc�!", "The account is already in use, report if it's not you!");
                fAcc.peer?.Disconnect();
                return;
            }

            acc.peer = peer;            
            Account.Items.Add(acc);

            // Verifica se os personagens ainda existem
            bool isUpdate = false;
            for(int i = 0; i < Constants.MAX_CHARACTERS; i++)
                if (acc.Characters[i].Length > 0 && !Character.Exist(acc.Characters[i]))
                {
                    acc.Characters[i] = "";
                    isUpdate = true;
                }

            // Salva a conta atualizada!
            if (isUpdate)
                Account.Save(acc);

            // Troca a cena            
            Sender.UpdateClass(peer);
            Sender.UpdateCharacters(peer);
            Sender.ChangeToSelectCharacter(peer);
        }

        /// <summary>
        /// Registra uma nova conta
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void Register(NetPeer peer, NetDataReader buffer)
        {
            var accName = buffer.GetString();
            var accPwd = buffer.GetString();

            // Verifica se j� existe a conta
            if (Account.Exist(accName))
            {
                Sender.Alert(peer, $"O nome {accName} n�o est� disponivel!", $"The name {accName} is not available!");
                return;
            }

            // Cria a conta
            var acc = new Account();
            acc.Name = accName;
            acc.Password = accPwd;
            Account.Save(acc);

            Sender.Alert(peer, $"A conta {accName} foi criada com sucesso!", $"The account {accName} has been successfully created!");
        }
    }
}
