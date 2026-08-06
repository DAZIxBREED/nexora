# Platform support

The same repository must open and compile in Unity 2022.3.22f1 on Windows and Linux.

Runtime content targets:

- Windows / PCVR
- Linux development through the native Unity Editor
- Android / Quest
- iOS mobile content

Repository rules:

- no absolute machine paths
- case-correct asset paths
- LF source files
- no registry dependency
- no Windows-only editor tools without Linux equivalents
- platform-specific behavior guarded with Unity compile symbols
