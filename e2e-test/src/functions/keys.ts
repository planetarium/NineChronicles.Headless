import { RawPrivateKey } from '@planetarium/account';
import { createAccount } from '@planetarium/account-raw';
import { adminAddress, adminPrivateKey, adminPublicKey } from './config.js';

export const adminKey = {
  privateKey: adminPrivateKey,
  publicKey: adminPublicKey,
  address: adminAddress,
  account: createAccount(adminPrivateKey),
};

export const generateKeys = async () => {
  const rawPrivateKey = RawPrivateKey.generate();
  const account = createAccount(rawPrivateKey.toBytes());
  const address = await rawPrivateKey.getAddress();
  const publicKey = await rawPrivateKey.getPublicKey();
  const privateKey = [...rawPrivateKey.toBytes()]
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('');

  return {
    privateKey,
    address: address.toString(),
    publicKey: publicKey.toHex('compressed'),
    account,
  };
};
