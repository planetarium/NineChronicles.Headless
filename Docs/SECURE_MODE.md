# Secure mode

Since [#230], `feat(graphql): provide secret token used in policy`, it has started to provide a mode to turn on authorization.
The mode makes important types need to authenticate with HTTP `Authorization` header. 

## Usage

To turn on the mode, you must run the headless application with `--graphql-secret-token-path` option, and passing the path to store secure token in local storage.
Then, there will be random 40 bytes token encoded by base64 at the path you passed. And you should read the secret token and request with `Authorization: Basic <SECRET_TOKEN>` header.   

### Example (/w GraphQL Playground)

It assumes you cloned this repository and it will work on the cloned directory.

```
$ docker run -p NineChronicles.Headless -- \
    # other options
    --graphql-server \
    --graphql-host localhost \
    --graphql-port <PORT> \
    --graphql-secret-token-path $PWD/secret-token
```

Then, there will be the secret token at `$PWD/secret-token`.

```
$ cat $PWD/secret-token
Jhlgn875txcwD5rKP38bs8ZbvYxnm3hliqYjNjETH8Bo6aX4Cbf6Ng==
``` 

Though I attached the secret token, it will be generated randomly for each running headless.
When you request mutations or some queries to GraphQL endpoint, you must do it with `Authorization: Basic <SECRET_TOKEN>`.
If you use GraphQL Playground, you can attach the header with *HTTP Headers* function placed on the left bottom box.

![image](https://user-images.githubusercontent.com/26626194/106431958-5bf98400-64b1-11eb-9121-12547c9bd9ed.png)

[#230]: https://github.com/planetarium/NineChronicles.Headless/pull/230
