# A .NET Core cross-platform console app - TOTP (2FA) Manager with encrypted file storage #

This system requires the 2FA Base32 secret text which is available (along with a QR code) when you setup 2FA with an application.

Encryption: AES (MODE_CBC) with HMAC authentication based on https://gist.github.com/jbtule/4336842

The encrypted data file is compatible with a Python version of this app at: https://gist.github.com/bifter/9f336911c83cbad34eba502850272c91

The data file is an encrypted json file containing the totp data

## Setup ##
1. Update the FILE_PATH variable to the path you want the data file to be stored in.
2. Add an initial totp item using: -pw {password to encrypt/decrypt the data file } -a {name of app i.e. 'Google'} {Base32 secret text} [Number of totp digits (defaults to 6)]

## Basic usage ##
To view totp tokens: -pw {password}
Add an item: -pw {password to encrypt/decrypt the data file } -a {name of app i.e. 'Google'} {Base32 secret text} [Number of totp digits (defaults to 6)]
Edit item: -pw -u -id {ID of item} [-title {Name of app}] [-secret {2FA secret}] [-digits {Number of digits}]
Remove an totp item: -pw {password} -del -id {ID of app - ID is shown in the list}
Update password: -pw {password} -pu {new password}
See help: -hh


