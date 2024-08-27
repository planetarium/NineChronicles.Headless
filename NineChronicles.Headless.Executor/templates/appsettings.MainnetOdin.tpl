{
    "Headless": {
        "AppProtocolVersionString": "${Apv}",
        "StoreType": "rocksdb",
        "StorePath": "${StorePath}",
        "NoMiner": true,
        "GenesisBlockPath": "https://release.nine-chronicles.com/genesis-block-9c-main",
        "IceServerStrings": [
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us.planetarium.dev:3478",
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us2.planetarium.dev:3478",
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us3.planetarium.dev:3478",
            "turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us5.planetarium.dev:3478"
        ],
        "PeerStrings": [
            "027bd36895d68681290e570692ad3736750ceaab37be402442ffb203967f98f7b6,9c-main-tcp-seed-1.planetarium.dev,31234",
            "02f164e3139e53eef2c17e52d99d343b8cbdb09eeed88af46c352b1c8be6329d71,9c-main-tcp-seed-2.planetarium.dev,31234",
            "0247e289aa332260b99dfd50e578f779df9e6702d67e50848bb68f3e0737d9b9a5,9c-main-tcp-seed-3.planetarium.dev,31234"
        ],
        "TrustedAppProtocolVersionSignerStrings": [
            "030ffa9bd579ee1503ce008394f687c182279da913bfaec12baca34e79698a7cd1"
        ],
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
