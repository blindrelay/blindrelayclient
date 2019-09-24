# Welcome to blindrelay!
Download and decryption API source code, example, and specification.

.NET Core source code and example coming soon. 

Used in conjuction with the Windows 10 blindrelay app (coming soon in the Microsoft App Store) to enable integration and automation with internal backend systems. 

Visit https://blindrelay.com for more information.

## blindrelay Architecture Overview
Blindrelay uses several Azure services (including Functions, Blobs, Queues, and SignalR) and a Windows 10 Universal App to orchestrate end-to-end encryption groups. With an optional API Key, developers can develop backend .NET Core (or other language) code to integrate downloading and decryption in backend systems.

![Image of blindrelay group sharing](https://blindrelay.com/media/blindrelay-multiple-subscribers-multiple-publishersubscribers.png)


## blindrelay CryptoBuffer File Format
Below is the specification for encrypted blindrelay files.
The example source code and library handle parsing, but this is provided for reference to support additional languages.

### Bytes

Field | Length (bytes)
------------ | ------------
Length | 4
Data Bytes | Variable Length in bytes

### String
Field | Length (bytes)
------------ | ------------
Length | 4
UTF-8 Characters | Variable Length in bytes

### CryptoBuffer File
Field | Type | Length (bytes or characters) | Contents or Purpose
------------ | ------------ | ------------ | ------------
PayloadLength | DWORD | 8 | Total length of file
Magic | Bytes | 4 chars | B,R,P,K
Signature | Bytes | 32 | HMACSHA256 signature of file contents
EncryptionAlgorithm | String | Variable | AES-256-CBC-PKCS7
CreatedBy | Bytes | Variable | ID of creator
Id | String | Variable | General identifier
KeyId | String | Variable | blindrelay ID of encryption key
Purpose | String | Variable | Generally the Group ID. Only keys of Purpose can decrypt file
MimeType | String | Variable | Mime-type of plaintext (not encrypted)
Created | DWORD | 8 | Unix time in milliseconds
Iv | Bytes | Variable | Initialization vector pertaining to EncryptionAlgorithm
CipherMetadata | Bytes | Variable | Encrypted metadata associated with ciphertext
CipherText | Bytes | Variable | File data encrypted with EncryptionAlgorithm
