﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network;
using PNet;
using PNetS.Utils;

namespace PNetS
{
    /// <summary>
    /// Network view, but for scene objects
    /// </summary>
    public class NetworkedSceneObjectView : Component
    {
        internal Room room;

        /// <summary>
        /// The scene/room Network ID of this item. Should only be one per room
        /// </summary>
        public ushort NetworkID { get; internal set; }

        private List<NetConnection> connections = new List<NetConnection>();

        /// <summary>
        /// update the connections this should send to when rpc or serializing. usually you shouldn't use this
        /// </summary>
        public void UpdateConnections()
        {
            connections = room.players.Select(p => p.RoomConnection).ToList();
        }

        #region RPC Processing

        readonly Dictionary<byte, RPCProcessor> _rpcProcessors = new Dictionary<byte, RPCProcessor>();

        /// <summary>
        /// Subscribe to an rpc
        /// </summary>
        /// <param name="rpcID">id of the rpc</param>
        /// <param name="rpcProcessor">action to process the rpc with</param>
        /// <param name="overwriteExisting">overwrite the existing processor if one exists.</param>
        /// <param name="defaultContinueForwarding">default info.continueForwarding value</param>
        /// <returns>Whether or not the rpc was subscribed to. Will return false if an existing rpc was attempted to be subscribed to, and overwriteexisting was set to false</returns>
        public bool SubscribeToRPC(byte rpcID, Action<NetIncomingMessage, NetMessageInfo> rpcProcessor, bool overwriteExisting = true, bool defaultContinueForwarding = true)
        {
            if (rpcProcessor == null)
                throw new ArgumentNullException("rpcProcessor", "the processor delegate cannot be null");
            if (overwriteExisting)
            {
                _rpcProcessors[rpcID] = new RPCProcessor(rpcProcessor, defaultContinueForwarding);
                return true;
            }
            else
            {
                if (_rpcProcessors.ContainsKey(rpcID))
                {
                    return false;
                }
                else
                {
                    _rpcProcessors.Add(rpcID, new RPCProcessor(rpcProcessor, defaultContinueForwarding));
                    return true;
                }
            }
        }

        /// <summary>
        /// Unsubscribe from an rpc
        /// </summary>
        /// <param name="rpcID"></param>
        public void UnsubscribeFromRPC(byte rpcID)
        {
            _rpcProcessors.Remove(rpcID);
        }

        internal void CallRPC(byte rpcID, NetIncomingMessage message, NetMessageInfo info)
        {
            RPCProcessor processor;
            if (_rpcProcessors.TryGetValue(rpcID, out processor))
            {
                info.continueForwarding = processor.DefaultContinueForwarding;

                if (processor.Method != null)
                    processor.Method(message, info);
                else
                {
                    Debug.LogWarning("RPC processor for {0} was null. Automatically cleaning up. Please be sure to clean up after yourself in the future.", rpcID);
                    _rpcProcessors.Remove(rpcID);
                }
            }
            else
            {
                Debug.LogWarning("NetworkedSceneView {1} received unhandled RPC {0}", rpcID, NetworkID);
                info.continueForwarding = false;
            }
        }

        #endregion

        /// <summary>
        /// Send an rpc to all in the room.
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="args"></param>
        public void RPC(byte rpcID, params INetSerializable[] args)
        {
            if (connections.Count == 0)
                return;

            var size = 3;
            RPCUtils.AllocSize(ref size, args);

            var message = room.Peer.CreateMessage(size);
            message.Write((ushort)NetworkID);
            message.Write(rpcID);
            RPCUtils.WriteParams(ref message, args);
            room.Peer.SendMessage(message, connections, NetDeliveryMethod.ReliableOrdered, Channels.OBJECT_RPC);
        }
    }
}
