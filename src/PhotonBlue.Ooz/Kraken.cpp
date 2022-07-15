#include "Kraken.h"
#include "../ooz/ooz.h"

int PhotonBlue::Ooz::Kraken::Decompress(array<uint8_t>^ src, const size_t src_len, array<uint8_t>^ dst, const size_t dst_len)
{
    const pin_ptr<const uint8_t> s = &src[0];
    const pin_ptr<uint8_t> d = &dst[0];
    return Kraken_Decompress(s, src_len, d, dst_len);
}
