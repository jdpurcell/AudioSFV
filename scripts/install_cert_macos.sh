#!/usr/bin/env bash

if [[ -n "$APPLE_DEVID_APP_CERT_DATA" ]]; then
    CODESIGN_CERT_PATH=$RUNNER_TEMP/codesign.p12
    KEYCHAIN_PATH=$RUNNER_TEMP/codesign.keychain-db
    KEYCHAIN_PASS=$(uuidgen)

    echo -n "$APPLE_DEVID_APP_CERT_DATA" | base64 --decode -o "$CODESIGN_CERT_PATH"

    security create-keychain -p "$KEYCHAIN_PASS" "$KEYCHAIN_PATH"
    security unlock-keychain -p "$KEYCHAIN_PASS" "$KEYCHAIN_PATH"
    security import "$CODESIGN_CERT_PATH" -P "$APPLE_DEVID_APP_CERT_PASS" -A -t cert -f pkcs12 -k "$KEYCHAIN_PATH"
    security set-key-partition-list -S apple-tool:,apple: -s -k "$KEYCHAIN_PASS" "$KEYCHAIN_PATH"
    security list-keychains -d user -s "$KEYCHAIN_PATH"

    CODESIGN_CERT_NAME=$(openssl pkcs12 -in "$CODESIGN_CERT_PATH" -passin pass:"$APPLE_DEVID_APP_CERT_PASS" -nokeys -clcerts -info 2>&1 | openssl x509 -noout -subject -nameopt multiline | grep commonName | awk -F'= ' '{print $2}')
    echo "CODESIGN_CERT_NAME=$CODESIGN_CERT_NAME" >> "$GITHUB_ENV"
    echo "SIGN_AND_NOTARIZE=true" >> "$GITHUB_ENV"
fi
