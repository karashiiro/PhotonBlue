#pragma once
#include <cstdint>

namespace PhotonBlue
{
    namespace Ooz
    {
        public ref class Kraken sealed
        {
        public:
            static int Decompress(array<uint8_t>^ src, size_t src_len, array<uint8_t>^ dst, size_t dst_len);
        };
    }
}
