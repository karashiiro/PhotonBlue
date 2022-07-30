# Photon Blue
Hardware-accelerated library for fast index and lookup operations on PSO2's game data. Visit the [documentation website](https://karashiiro.moe/PhotonBlue/) (WIP) for usage information.

## System requirements
* Windows x64
* .NET 6 Runtime
* DirectX 12

## Performance
(TODO: Benchmarks)

Performance is generally limited by your disk read capacity. Data can only be read at a certain rate that is specific to the disk.
This read rate is shared *by all active processes*. You can view this in Task Manager, under the "Performance" tab. If your installation disk's
usage is at 100%, Photon Blue will not be able to read data any faster. Repeated indexing runs may be substantially faster than the first run
after a boot; this is because file data may be cached in memory by the operating system, allowing it to be read without making requests to the disk.

File processing work is spread across the CPU and the GPU, with the CPU handling most of the light processing steps, and the GPU handling file decryption.
Because of this, on HDDs, most of the indexing time is spent waiting for more data to be available.

To accelerate read performance, use an SSD or some faster storage medium. If you have an HDD, defragmenting it may provide a very small performance boost
(not taking into account caching, as noted above). Game data may also be spread across multiple drives using symlinks, allowing
load to be distributed across them. Given that NGS is significantly smaller than the old PSO2, it may be a good idea to store that on an SSD, symlinking
the `win32reboot` folder back into `pso2_bin/data`.

## Acknowledgements
Photon Blue copies/builds on prior work in both the PSO2 and FFXIV development communities,
among other knowledge bases.

* A bunch of structural features are blatantly ripped-off of [Lumina](https://github.com/NotAdam/Lumina).
* NIFL file structure information is adapted from NIFLnew.
* ICE file structure information is referenced from [ZamboniLib](https://github.com/Shadowth117/ZamboniLib).
* PRS implementation is adapted from [ZamboniLib](https://github.com/Shadowth117/ZamboniLib).
* Additional PRS information is copied and/or referenced from [playegs/prs](https://github.com/playegs/prs) and [HybridEidolon/rust-ages-prs](https://github.com/HybridEidolon/rust-ages-prs).
* LZ77 algorithm information is referenced from [MSDN](https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wusp/fb98aa28-5cd7-407f-8869-a6cef1ff1ccb).
* LZSS algorithm information is referenced from [Wikipedia](https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Storer%E2%80%93Szymanski).
* Blowfish key extraction and usage is copied from [ZamboniLib](https://github.com/Shadowth117/ZamboniLib).
* Blowfish implementation is adapted from [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher).
* CRC32 implementation is copied from [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher).
