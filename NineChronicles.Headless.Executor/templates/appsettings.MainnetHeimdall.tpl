{
    "Headless": {
        "AppProtocolVersionString": "${Apv}",
        "StoreType": "rocksdb",
        "StorePath": "${StorePath}",
        "GenesisBlockPath": "https://planets.nine-chronicles.com/planets/0x000000000001/genesis",
        "IceServerStrings": [
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us.nine-chronicles.com:3478",
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us2.nine-chronicles.com:3478",
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us3.nine-chronicles.com:3478",
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us4.nine-chronicles.com:3478",
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us5.nine-chronicles.com:3478"
        ],
        "PeerStrings": [
            "03380b4ba8722057c9b4d8594f8de9481eb296aba4b3c168666f57b17596452ae7,heimdall-seed-1.nine-chronicles.com,31234"
        ],
        "TrustedAppProtocolVersionSignerStrings": [
            "031c5b9cb11b1cc07f8530599fa32338967e41cb364cca552a34ad2157ccb237bf"
        ],
        "NoMiner": true,
        "GraphQLServer": true,
        "GraphQLHost": "localhost",
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
