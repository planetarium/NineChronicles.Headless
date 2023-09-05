import axios from 'axios';

export const executeGqlQuery = async (
  network: string,
  query: string,
  variables?: unknown,
) => {
  try {
    return await axios.post(
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
