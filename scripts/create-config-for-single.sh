#!/bin/bash

read -p "Enter the private key: " privateKey
read -p "Enter the public key: " publicKey
read -p "Enter the address: " address

cat <<EOL >config.json
{
    "\$schema": "./config.schema.json",
    "data": {
        "tablePath": "./Lib9c/Lib9c/TableCSV"
    },
    "admin": {
        "activate": true,
        "address": "$address",
        "validUntil": 1000000
    },
    "currency": {
        "initialMinter": "$privateKey",
        "initialCurrencyDeposit": [
            {
                "address": "$address",
                "amount": 1000000,
                "start": 0,
                "end": 0
            }
        ]
    },
    "initialValidatorSet": [
        {
            "publicKey": "$publicKey",
            "power": 1
        }
    ],
    "initialMeadConfigs": [
        {
            "address": "$address",
            "amount": "1000000"
        }
    ],
    "initialPledgeConfigs": []
}
EOL

echo "config.json has been created successfully."
