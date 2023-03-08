import tomllib
import tempfile
import os
import subprocess
import shutil

from typing import TypedDict

from git import Repo


class VersionSpec(TypedDict):
    ref: str


class BuildConfiguration(TypedDict):
    output_path: str
    repository_url: str


class Configuration(TypedDict):
    versions: dict[str, VersionSpec]
    config: BuildConfiguration


conf: Configuration
with open(".versions.toml", "rb") as f:
    conf = tomllib.load(f)


output_path = conf["config"]["output_path"]
repository_url = conf["config"]["repository_url"]

tmpdir = tempfile.mkdtemp()

if os.path.exists(output_path):
    shutil.rmtree(output_path)
os.mkdir(output_path)

repo = Repo.clone_from(repository_url, tmpdir, multi_options=["--recurse-submodules"])
for version, spec in conf["versions"].items():
    ref = spec["ref"]

    repo.head.reset(ref)
    publish_directory = os.path.join(output_path, version)
    subprocess.run(
        [
            "dotnet",
            "publish",
            "Lib9c/Lib9c.csproj",
            "-c",
            "Release",
            "-o",
            publish_directory,
        ],
        cwd=tmpdir,
        capture_output=True,
    )
