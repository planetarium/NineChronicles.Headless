@ECHO OFF

set PRIVATEKEY="XXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
set SNAPSHOTDIR="C:\Users\XXXXXX\AppData\Local\planetarium2\9c-main-partition"

ECHO "Starting Nine Chronicles - HEADLESS..."
NineChronicles.Headless.Executable.exe ^
-V=100049/6ec8E598962F1f475504F82fD5bF3410eAE58B9B/MEQCIH8DnPXi9kVUlFT6hcThGGX.id5kRjjUR6nezFA1eJJ2AiA23.ARHp.rlY+StkB7JmRG2IKw+c05Qdz1x4OaZragIw==/ZHUxNjpXaW5kb3dzQmluYXJ5VXJsdTU2Omh0dHBzOi8vZG93bmxvYWQubmluZS1jaHJvbmljbGVzLmNvbS92MTAwMDQ5L1dpbmRvd3MuemlwdTk6dGltZXN0YW1wdTEwOjIwMjEtMDYtMTVl ^
-G=https://download.nine-chronicles.com/genesis-block-9c-main ^
-D=5000000 ^
--store-type=rocksdb ^
-I=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us4.planetarium.dev:3478 ^
-I=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us5.planetarium.dev:3478 ^
--peer=027bd36895d68681290e570692ad3736750ceaab37be402442ffb203967f98f7b6,9c-main-seed-1.planetarium.dev,31234 ^
--peer=02f164e3139e53eef2c17e52d99d343b8cbdb09eeed88af46c352b1c8be6329d71,9c-main-seed-2.planetarium.dev,31234 ^
--peer=0247e289aa332260b99dfd50e578f779df9e6702d67e50848bb68f3e0737d9b9a5,9c-main-seed-3.planetarium.dev,31234 ^
-T=03eeedcd574708681afb3f02fb2aef7c643583089267d17af35e978ecaf2a1184e ^
--rpc-server ^
--rpc-listen-host=127.0.0.1 ^
--rpc-listen-port=23146 ^
--graphql-server ^
--graphql-host=localhost ^
--graphql-port=23062 ^
--workers=500 ^
--confirmations=2 ^
--tip-timeout=120 ^
--aws-region=ap-northeast-2 ^
--miner-private-key=%PRIVATEKEY% ^
--store-path=%SNAPSHOTDIR%