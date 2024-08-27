{
    "Headless": {
        "GenesisBlockPath": "${GenesisBlockPath}",
        "AppProtocolVersionString": "1/b4179Ad0d7565A6EcFA70d2a0f727461039e0159/MEUCIQDvIIp8IKCpjKojE8LzgYZzeRg9fUPl.sWHrowzHhmrxgIgBhTkSRc8BHXZwwIAwBQN8J3wGlAbOD7FRyp8bA6OH6Y=",
        "StoreType": "rocksdb",
        "StorePath": "${StorePath}",
        "NoMiner": false,
        "MinerPrivateKeyString": "${MinerPrivateKeyString}",
        "ConsensusPrivateKeyString": "${ConsensusPrivateKeyString}",
        "ConsensusSeedStrings": [
            "${ConsensusSeedPublicKey},127.0.0.1,31588"
        ],
        "ConsensusPort": 31588,
        "Host": "127.0.0.1",
        "Port": 31234,
        "RpcServer": true,
        "RpcListenHost": "127.0.0.1",
        "RpcListenPort": 31238,
        "RpcRemoteServer": true,
        "GraphQLServer": true,
        "GraphQLHost": "127.0.0.1",
        "GraphQLPort": 31280,
        "NoCors": true,
        "ChainTipStaleBehaviorType": "reboot"
    },
    "Serilog": {
        "Using": [
            "Serilog.Expressions",
            "Serilog.Sinks.Console",
            "Serilog.Sinks.RollingFile"
        ],
        "MinimumLevel": "Debug",
        "WriteTo": [
            {
                "Name": "Logger",
                "Args": {
                    "configureLogger": {
                        "WriteTo": [
                            {
                                "Name": "Console",
                                "Args": {
                                    "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact",
                                    "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] [{Source}] {Message:lj}{NewLine}{Exception}"
                                }
                            }
                        ],
                        "Filter": [
                            {
                                "Name": "ByIncludingOnly",
                                "Args": {
                                    "expression": "Source is not null"
                                }
                            }
                        ]
                    }
                }
            },
            {
                "Name": "Logger",
                "Args": {
                    "configureLogger": {
                        "WriteTo": [
                            {
                                "Name": "Console",
                                "Args": {
                                    "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact",
                                    "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                                }
                            }
                        ],
                        "Filter": [
                            {
                                "Name": "ByExcluding",
                                "Args": {
                                    "expression": "Source is not null"
                                }
                            }
                        ]
                    }
                }
            }
        ],
        "Filter": [
            {
                "Name": "ByExcluding",
                "Args": {
                    "expression": "SourceContext = 'Libplanet.Stun.TurnClient'"
                }
            }
        ]
    },
    "Logging": {
        "LogLevel": {
            "Microsoft": "None"
        }
    }
}
