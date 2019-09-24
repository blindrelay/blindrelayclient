# Welcome to blindrelay!
Download and decryption API source code, example, and specification.

.NET Core source code and example coming soon. 

Used in conjuction with the Windows 10 blindrelay app (coming soon in the Microsoft App Store) to enable integration and automation with internal backend systems. 

Visit https://blindrelay.com for more information.

## blindrelay Architecture Overview
Blindrelay uses several Azure services (including over 70 Azure Functions, Cosmos DB, Blobs, Queues, and SignalR) and a Windows 10 Universal App to orchestrate end-to-end encryption groups. With an optional API Key, developers can develop backend .NET Core (or other language) code to integrate downloading and decryption in automated backend systems.

![Image of blindrelay group sharing](https://blindrelay.com/media/blindrelay-multiple-subscribers-multiple-publishersubscribers.png)

End-to-end encryption groups are created simply by inviting other users to a group via email addresses. Group members can be invited as publishers, subscribers, or both. 

Group formation occurs as follows in the blindrelay Windows 10 app (either by using the GUI or watched folders):
1. General group settings are set and member invitee email addresses are specified.
2. The group AES 256 encryption key is randomly generated (using CSRNG) and group member invitees get invited by a SignlaR and/or an email (optional) invitation.
3. Upon acceptance, group members receive the AES key via the member's RSA 4096 public key.

After a group is created, files can be published to the group. The steps in group file publishing are:
1. Select one or more files to publish.
2. File gets AES 256 encrypted and HMACSHA256 signed (on publisher's device) with the group AES key in the CryptoBuffer format (see below).
3. Encrypted file payload gets placed in an Azure blob and group subscriber records are recorded in Cosmos DB.
4. Group subscribers are notified via SignalR that there are files to download.

The steps in group file download by subscribers are:
1. Using the blindrelay app or custom backend code (via an API Key), files ready to download are streamed to subscriber devices, still encrypted.
2. If using the app, the user has control when the downloaded files get decrypted. Additionally, if the API is used, the custom code can download and decrypt the file(s) after getting notified via SignalR.
3. File decryption, using the group AES key, happens after the file has been downloaded.

For more detailed information, visit https://blindrelay.com/HowBlindrelayWorks

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
