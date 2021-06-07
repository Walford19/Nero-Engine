using LiteNetLib;
using LiteNetLib.Utils;
using Nero.Server.Map;
using Nero.Server.Player;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nero.Server.Network
{
    static class Sender
    {
        enum Packets
        {
            Alert, ChangeToSelectCharacter, UpdateClass,
            UpdateCharacters, ChangeToGameplay, UpdateMyCharacter,
            UpdateCharacterPosition, CheckMapRevision, MapData,
        }

        /// <summary>
        /// Envia o mapa
        /// </summary>
        /// <param name="peer"></param>
        public static void MapData(NetPeer peer)
        {
            var player = Character.Find(peer);
            var buffer = Create(Packets.MapData);
            buffer.Put(player.MapID);
            buffer.Put(JsonConvert.SerializeObject(MapInstance.Items[player.MapID]));
            SendTo(player, buffer);
        }

        /// <summary>
        /// Verifica a revis�o do mapa
        /// </summary>
        /// <param name="player"></param>
        public static void CheckMapRevision(Character player)
        {
            var buffer = Create(Packets.CheckMapRevision);
            buffer.Put(player.MapID);
            buffer.Put(MapInstance.Items[player.MapID].Revision);
            SendTo(player, buffer);
        }

        /// <summary>
        /// Atualiza a posi��o do personagem
        /// </summary>
        /// <param name="c"></param>
        public static void UpdateCharacterPosition(Character player)
        {
            var buffer = Create(Packets.UpdateCharacterPosition);
            buffer.Put(player.Name);
            buffer.Put(player.Position);
            SendToInstance(player, buffer);
        }

        /// <summary>
        /// Dados de personagem
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="buffer"></param>
        static void UpdateCharacterPacket(Character c, NetDataWriter buffer)
        {
            buffer.Put(c.Name);
            buffer.Put(c.ClassID);            
            buffer.Put(c.SpriteID);
            buffer.Put(c.Level);
            buffer.Put(c.Position);
            buffer.Put((byte)c.AccessLevel);
        }

        /// <summary>
        /// Atualiza meu personagem
        /// </summary>
        /// <param name="peer"></param>
        public static void UpdateMyCharacter(NetPeer peer)
        {
            var buffer = Create(Packets.UpdateMyCharacter);
            var c = Character.Find(peer);

            UpdateCharacterPacket(c, buffer);

            SendTo(peer, buffer);
        }

        /// <summary>
        /// Muda a cena para gameplay
        /// </summary>
        /// <param name="peer"></param>
        public static void ChangeToGameplay(NetPeer peer)
        {
            SendTo(peer, Create(Packets.ChangeToGameplay));
        }

        /// <summary>
        /// Envia os personagens da conta
        /// </summary>
        /// <param name="peer"></param>
        public static void UpdateCharacters(NetPeer peer)
        {
            var acc = Account.Find(peer);
            var buffer = Create(Packets.UpdateCharacters);

            foreach(var i in acc.Characters)
            {
                buffer.Put(i);
                if (i.Length > 0)
                {
                    var c = Character.Load(i);
                    buffer.Put(c.SpriteID);                    
                }
            }

            SendTo(peer, buffer);
        }

        /// <summary>
        /// Atualiza as classes
        /// </summary>
        /// <param name="peer"></param>
        public static void UpdateClass(NetPeer peer)
        {
            var buffer = Create(Packets.UpdateClass);
            buffer.Put(CharacterClass.Items.Count);
            foreach(var i in CharacterClass.Items)
            {
                buffer.PutArray(i.Name);
                buffer.PutArray(i.Description);
                buffer.PutArray(i.StatPrimary);
                buffer.PutArray(i.MaleSprite);
                buffer.PutArray(i.FemaleSprite);
            }
            SendTo(peer, buffer);
        }

        /// <summary>
        /// Muda a cena para sele��o de personagens
        /// </summary>
        /// <param name="peer"></param>
        public static void ChangeToSelectCharacter(NetPeer peer)
        {
            SendTo(peer, Create(Packets.ChangeToSelectCharacter));
        }

        /// <summary>
        /// Envia um alerta
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="text"></param>
        public static void Alert(NetPeer peer, params string[] text)
        {
            var buffer = Create(Packets.Alert);
            buffer.PutArray(text);
            SendTo(peer, buffer);
        }

        /// <summary>
        /// Cria um pacote
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        static NetDataWriter Create(Packets packet)
        {
            var buffer = new NetDataWriter();
            buffer.Put((short)packet);
            return buffer;
        }

        /// <summary>
        /// Envia o pacote
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="buffer"></param>
        static void SendTo(NetPeer peer, NetDataWriter buffer)
        {
            if (peer == null || peer.ConnectionState != ConnectionState.Connected)
                return;

            peer.Send(buffer, DeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Envia um pacote
        /// </summary>
        /// <param name="player"></param>
        /// <param name="buffer"></param>
        static void SendTo(Character player, NetDataWriter buffer)
            => SendTo(player.peer, buffer);        

        /// <summary>
        /// Envia o pacote para a Instancia
        /// </summary>
        static void SendToInstance(Character player, NetDataWriter buffer)
        {
            var lst = Character.Items.Where(i => i.GetInstance() == player.GetInstance()).ToList();

            foreach (var i in lst)
                SendTo(i.peer, buffer);
        }

        /// <summary>
        /// Envia o pacote para a Instancia
        /// </summary>
        static void SendToInstance(IInstance instance, NetDataWriter buffer)
        {
            var lst = Character.Items.Where(i => i.GetInstance() == instance).ToList();

            foreach (var i in lst)
                SendTo(i.peer, buffer);
        }

        /// <summary>
        /// Envia o pacote para a Instancia, mas sem para si
        /// </summary>
        static void SendToInstanceBut(Character controller, NetDataWriter buffer)
        {
            var lst = Character.Items.Where(i => i != controller && i.GetInstance() == controller.GetInstance()).ToList();

            foreach (var i in lst)
                SendTo(i.peer, buffer);
        }

        /// <summary>
        /// Envia o pacote para todos
        /// </summary>
        /// <param name="buffer"></param>
        static void SendToAll(NetDataWriter buffer)
        {
            Socket.Device.SendToAll(buffer, DeliveryMethod.ReliableOrdered);
        }
    }
}
