# Pluggable Lib9c

## Prepare Lib9c dlls

### Check .versions.toml configuration file

```
[versions]
v100370 = { ref = "v100370" }
v100371 = { ref = "v100371" }

[config]
output_path = "/tmp/testdlls"
repository_url = "https://github.com/planetarium/lib9c"
```

### Run prepare-pluggable-lib9c.py


```bash
python --version  # Requires >=3.11
python -m pip install GitPython
python prepare-pluggable-lib9c.py

ls /tmp/testdlls  # Check the `output_path` you configured.
```
