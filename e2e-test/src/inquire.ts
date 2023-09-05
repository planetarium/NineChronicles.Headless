import { executeGqlQuery } from 'src/gql';
import { network } from './config';

export const getGoldBalance = async (address: string) => {
  const query = `{ goldBalance(address: "${address}") }`;
  const response = await executeGqlQuery(network, query);
  const result = response?.data?.data?.goldBalance;
  if (!result) return;

  return parseInt(result, 10);
};

export const getMeadBalance = async (address: string) => {
  const query = `
  {
    stateQuery {
      balance(
        address: "${address}",
        currency: {
          ticker: "Mead",
          decimalPlaces: 18,
          minters: []
        }
      ) {
        quantity
      }
    }
  }`;
  const response = await executeGqlQuery(network, query);
  const result = response?.data?.data?.stateQuery?.balance?.quantity;
  if (!result) return;

  return parseInt(result, 10);
};
