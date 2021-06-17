@ECHO OFF

set PRIVATEKEY="XXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
set SNAPSHOTDIR="C:\Users\XXXXXX\AppData\Local\planetarium2\9c-main-partition"
set THREADS=4

ECHO "Starting Nine Chronicles - HEADLESS..."
ECHO " --Dont forget to manually download snapshot"
ECHO " --Logging to file: headless.log (Tail this file for logging)"
ECHO "KEEP THIS WINDOW OPEN FOR MINER TO RUN"
app\NineChronicles.Headless.Executable.exe ^
-V=100050/6ec8E598962F1f475504F82fD5bF3410eAE58B9B/MEQCIHlwEelW7wDvvDS7nGfXWN8JVBPzMtZj83hysYT2hjRYAiBhF86LNJLrkWEtJXkIGMKtRaA4ZXC+utBq929AAVv.GQ==/ZHUxNjpXaW5kb3dzQmluYXJ5VXJsdTU2Omh0dHBzOi8vZG93bmxvYWQubmluZS1jaHJvbmljbGVzLmNvbS92MTAwMDUwL1dpbmRvd3MuemlwdTE0Om1hY09TQmluYXJ5VXJsdTU3Omh0dHBzOi8vZG93bmxvYWQubmluZS1jaHJvbmljbGVzLmNvbS92MTAwMDUwL21hY09TLnRhci5nenU5OnRpbWVzdGFtcHUyNToyMDIxLTA2LTE2VDEzOjA3OjUxKzAwOjAwZQ== ^
-G=https://download.nine-chronicles.com/genesis-block-9c-main ^
-D=5000000 ^
--store-type=rocksdb ^
--ice-server=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us1.planetarium.dev:3478 ^
--ice-server=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us2.planetarium.dev:3478 ^
--ice-server=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us3.planetarium.dev:3478 ^
--ice-server=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us4.planetarium.dev:3478 ^
--ice-server=turn://0ed3e48007413e7c2e638f13ddd75ad272c6c507e081bd76a75e4b7adc86c9af:0apejou+ycZFfwtREeXFKdfLj2gCclKzz5ZJ49Cmy6I=@turn-us5.planetarium.dev:3478 ^
--peer=027bd36895d68681290e570692ad3736750ceaab37be402442ffb203967f98f7b6,9c-main-seed-1.planetarium.dev,31234 ^
--peer=02f164e3139e53eef2c17e52d99d343b8cbdb09eeed88af46c352b1c8be6329d71,9c-main-seed-2.planetarium.dev,31234 ^
--peer=0247e289aa332260b99dfd50e578f779df9e6702d67e50848bb68f3e0737d9b9a5,9c-main-seed-3.planetarium.dev,31234 ^
--trusted-app-protocol-version-signer=03eeedcd574708681afb3f02fb2aef7c643583089267d17af35e978ecaf2a1184e ^
--graphql-server ^
--graphql-host=localhost ^
--graphql-port=23062 ^
--workers=500 ^
--confirmations=2 ^
--tip-timeout=120 ^
--graphql-server ^
--graphql-port=23062 ^
--aws-secret-key=lmgIuUDboAP6kHl2hpoZ4mvXkRPk+k5qj9vOvKq9 ^
--aws-access-key=AKIAUU3S3PEZFVKH626P ^
--aws-region=ap-northeast-2 ^
--miner-private-key=%PRIVATEKEY% ^
--store-path=%SNAPSHOTDIR% ^
--miner-count=%THREADS% ^
1> headless.log 2>&1