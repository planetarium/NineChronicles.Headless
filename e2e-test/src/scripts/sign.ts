import { signTransaction } from '@planetarium/sign';
import { adminKey } from '../functions/keys.js';

// eslint-disable-next-line @typescript-eslint/naming-convention, @typescript-eslint/no-unused-vars
const [_, __, tx] = process.argv;

const sign = async () => {
  const signed = await signTransaction(tx, adminKey.account);

  console.log(signed);
};

sign();
