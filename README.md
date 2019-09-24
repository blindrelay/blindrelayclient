# Welcome to blindrelay!

#blindrelay CryptoBuffer file format

##CryptoBuffer Bytes

Field | Length (bytes)
------------ | ------------
Length | 4
Data Bytes | Variable Length in bytes

## CryptoBuffer String
Field | Length (bytes)
------------ | ------------
Length | 4
UTF-8 Characters | Variable Length in bytes

Field | Type | Length (bytes or characters) | Contents or Purpose
------------ | ------------ | ------------ | ------------
PayloadLength | DWORD | 8 | Total length of file
Magic | String | 4 chars | B,R,P,K
Signature | Bytes | 32 | HMACSHA256 signature of file contents
EncryptionAlgorithm | String | Variable | "AES-256-CBC-PKCS7"
CreatedBy | Bytes | Variable | ID of creator
Id | String | Variable | General identifier
KeyId | String | Variable | blindrelay ID of encryption key
Purpose | String | Variable | Generally the Group ID. Only keys of Purpose can decrypt file
MimeType | String | Variable | Mime-type of plaintext
Created | DWORD | 8 | Unix time in milliseconds
Iv | Bytes | Variable | Initialization vector pertaining to EncryptionAlgorithm
CipherMetadata | Bytes | Variable | Encrypted metadata associated with ciphertext
CipherText | Bytes | Variable | File data encrypted with EncryptionAlgorithm
