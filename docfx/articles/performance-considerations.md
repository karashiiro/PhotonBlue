# Performance considerations
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