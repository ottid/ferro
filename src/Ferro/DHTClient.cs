using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Ferro {
    // Identifier for a DHT query that can be used as a dictionary key.
    public class DHTQueryKey {
        public IPEndPoint EP; // the ip and port of the dht node
        public byte[] Token; // the unique opaque token we sent with the query

        public override int GetHashCode() {
            // crappy but maybe adequate sum as hash. should be cached.
            return
                EP.Address.GetAddressBytes().Sum(x => x) +
                EP.Port +
                Token.Sum(x => x);
        }

        public override bool Equals(object obj) {
            if (!(obj != null && obj is DHTQueryKey)) {
                return false;
            }
            var other = obj as DHTQueryKey;
            return
                EP.Address.Equals(other.EP.Address) && 
                EP.Port.Equals(other.EP.Port) &&
                Token.SequenceEqual(other.Token);
        }

        public override string ToString() {
            return $"[{EP}/{String.Join(",", Token.Select(x => x.ToString()))}]";
        }
    }

    // TODO We need a utility to Sort a list of byte arrays using the BEP 5/Kad
    // XOR metric distance from a given target infohash, to use in all of these functions.

    public class DHTMessage {
        public Dictionary<byte[], dynamic> Data;
    }

    // A client (not server) for the mainline BitTorrent DHT.
    public class DHTClient
    {
        readonly byte[] NodeId;
        readonly IPEndPoint LocalEndPoint = new IPEndPoint(IPAddress.Any, 6881);
        private UDPSocket socket;

        readonly Task Listening;
        
        private HashSet<IPEndPoint> knownGoodNodes;

        // This is terrible.
        byte nextToken = 0;

        private Dictionary<DHTQueryKey, TaskCompletionSource<DHTMessage>> pendingQueries;

        public DHTClient() {
            pendingQueries = new Dictionary<DHTQueryKey, TaskCompletionSource<DHTMessage>>();

            NodeId = new byte[20].FillRandom();

            socket = new UDPSocket(LocalEndPoint);

            knownGoodNodes = new HashSet<IPEndPoint>();

            Listening = Task.Run(async () => {
                while (true) {
                    await Task.Delay(10);

                    try {
                        var response = await socket.ReceiveAsync();

                        var value = Bencoding.DecodeDict(response.Data);

                        var type = value.GetString("y");

                        switch (type) {
                            case "r": {
                                Console.WriteLine($"Got response message from {response.Source}.");

                                var key = new DHTQueryKey {
                                    Token = value.GetBytes("t"),
                                    EP = response.Source
                                };

                                Console.WriteLine("For query key: " + key);

                                if (pendingQueries.ContainsKey(key)) {
                                    var responseSource = pendingQueries[key];
                                    pendingQueries.Remove(key);

                                    responseSource.SetResult(new DHTMessage { Data = value });
                                    Console.WriteLine("Resolved pending task.");
                                } else {
                                    Console.WriteLine("But I wasn't expecting that!");
                                }

                                break;
                            }

                            case "e": {
                                Console.WriteLine($"Got error mesage from {response.Source}.");

                                var key = new DHTQueryKey {
                                    Token = value.GetBytes("t"),
                                    EP = response.Source
                                };

                                Console.WriteLine("For query key: " + key);

                                if (pendingQueries.ContainsKey(key)) {
                                    var responseSource = pendingQueries[key];
                                    pendingQueries.Remove(key);

                                    var errors = value.GetList("e");
                                    var code = (Int64) errors[0];
                                    var message = ((byte[]) errors[1]).FromASCII();
                                    
                                    var exception = new Exception($"{code} {message}");
                                    Console.WriteLine("Rejecting pending task.");
                                    responseSource.SetException(new Exception[] { exception });
                                } else {
                                    Console.WriteLine("But I wasn't expecting that!");
                                }

                                break;
                            }

                            case "q": {
                                Console.WriteLine($"Ignored query mesage from {response.Source}:\n{Bencoding.ToHuman(response.Data)}");
                                // do nothing because we're read-only
                                break;
                            }

                            default: {
                                Console.WriteLine($"Got unknown mesage from {response.Source}:\n{Bencoding.ToHuman(response.Data)}");
                                // maybe we could send an error?
                                break;
                            }
                        }
                    } catch (Exception ex) {
                        Console.WriteLine("Exception! " + ex);  
                    }
                }
            });
        }

        // Pings the DHT node at the given endpoint and returns its id, or throws an error.
        // If the node is pinged successfully, it adds it to routing table.
        public async Task<byte[]> Ping(IPEndPoint ep) {
            var token = new byte[]{nextToken++};

            var result = new TaskCompletionSource<DHTMessage>();
            var key = new DHTQueryKey { Token = token, EP = ep };
            pendingQueries[key] = result;

            Console.WriteLine($"Sending ping {key}...");
            sendPing(ep, token);

            var results = await result.Task;

            var nodeId = results.Data.GetDict("r").GetBytes("id");

            knownGoodNodes.Add(ep);

            return nodeId;
        }

        public async Task<List<IPEndPoint>> GetPeers(byte[] infohash) {
            // TODO: This is not complete.
            // This should, up to like 10 times or something, ask the closest
            // node to the target infohash if it knows any nodes closer to that,
            // until it gets the result (a list of peers, not nodes) or gives up
            // and returns an empty list or throws an exception.
            foreach (var node in knownGoodNodes) {
                var token = new byte[]{nextToken++};

                var result = new TaskCompletionSource<DHTMessage>();
                var key = new DHTQueryKey { Token = token, EP = node };
                pendingQueries[key] = result;

                Console.WriteLine($"Sending get_peers {key}...");
                sendGetPeers(node, token, infohash);

                var results = await result.Task;

                var nodesData = results.Data.GetDict("r").GetBytes("nodes");

                // We need to parse `nodesData` as NODES and ping them.

                return new List<IPEndPoint> {};
            }

            throw new Exception("had no good nodes to query");
        }

        void sendPing(IPEndPoint destination, byte[] token) {
            var dict = Bencoding.Dict();
            dict.Set("t", token);
            dict.Set("y", "q");
            dict.Set("q", "ping");
            dict.Set("ro", 1);
            var args = Bencoding.Dict();
            args.Set("id", NodeId);
            dict.Set("a", args);

            var encoded = Bencoding.Encode(dict);
            Console.WriteLine($"Sending ping to {destination}.");
            socket.SendTo(encoded, destination);
        }

        void sendGetPeers(IPEndPoint destination, byte[] token, byte[] infohash) {

            var dict = Bencoding.Dict();
            dict.Set("t", token);
            dict.Set("y", "q");
            dict.Set("q", "get_peers");
            dict.Set("ro", 1);
            var args = Bencoding.Dict();
            args.Set("id", NodeId);
            args.Set("info_hash", infohash);
            dict.Set("a", args);

            var encoded = Bencoding.Encode(dict);

            Console.WriteLine($"Sending get_peers to {destination}.");
            socket.SendTo(encoded, destination);
        }

        void sendFindNode() {
            throw new Exception("NOT IMPLEMENTED");
        }
    }
}