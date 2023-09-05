import * as dotenv from 'dotenv';

dotenv.config();

export const network = process.env.NETWORK as string;
export const log = process.env.LOG === 'true';
