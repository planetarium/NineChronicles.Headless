import * as dotenv from 'dotenv';

dotenv.config();

export const network = process.env.NETWORK as string;
export const log = process.env.LOG === 'true';

export const adminPrivateKey = process.env.ADMIN_PRIVATE_KEY as string;
export const adminPublicKey = process.env.ADMIN_PUBLIC_KEY as string;
export const adminAddress = process.env.ADMIN_ADDRESS as string;
