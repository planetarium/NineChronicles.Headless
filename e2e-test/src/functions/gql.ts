import axios from 'axios';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const executeGqlQuery = async <T = any>(
  network: string,
  query: string,
  variables?: unknown,
) => {
  try {
    return await axios.post<T>(
      `${network}/graphql`,
      JSON.stringify({
        query,
        variables,
      }),
      {
        headers: {
          'Content-Type': 'application/json',
        },
      },
    );
  } catch (e) {
    console.error(e);
  }
};
