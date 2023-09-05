import { RawPrivateKey } from '@planetarium/account';
import { createAccount } from '@planetarium/account-raw';
import { Account } from '@planetarium/sign';

export type Key = {
  publicKey: string;
  address: string;
  privateKey: string;
  account: Account;
};

export const adminAccountKey: Key = {
  privateKey: process.env.ADMIN_PRIVATE_KEY as string,
  address: process.env.ADMIN_ADDRESS as string,
  publicKey: process.env.ADMIN_PUBLIC_KEY as string,
  account: createAccount(process.env.ADMIN_PRIVATE_KEY as string),
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
