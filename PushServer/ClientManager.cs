using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PushServer{
    public static class ClientManager{
        private static ConcurrentDictionary<ulong,CallClient> ClientList = new ConcurrentDictionary<ulong,CallClient>();

        public static Task<bool> AddCallClient(CallClient client){
            return Task.Factory.StartNew(()=>{
                if(ClientList.ContainsKey(client.Id))
                {
                    return false;
                }
                ClientList.TryAdd(client.Id,client);
                return true;
            });
        }

        public static Task<bool> DelCallClient(ulong id){
            return Task.Factory.StartNew(()=>{
                CallClient outClient;
                return ClientList.TryRemove(id,out outClient);
            });
        }

        public static WeakReference<CallClient> GetCallClient(ulong id){
            CallClient outClient;
            if(ClientList.TryGetValue(id,out outClient)){
                return new WeakReference<CallClient>(outClient,false);
            }
            return null;
        }

        public static ICollection<ulong> GetCurrentAllKeys(){
            return ClientList.Keys;
        }
    }
}