# Microsoft Store Readiness

Verdict: **Store-risky**

Pure MSIX verdict: **Store-blocked for the current architecture**

## Why

DnsSwitcher is a privileged networking utility:
- it installs a Windows Service
- service installation requires administrator rights
- the main value of the product depends on changing system DNS settings
- Split DNS applies Windows NRPT rules, also privileged

Microsoft Store supports packaged apps and also supports MSI/EXE app submissions, but the installer must pass certification, support silent install, and be stable after submission.

References:
- Microsoft Store get started: https://learn.microsoft.com/windows/apps/publish/get-started
- MSI/EXE app submission: https://learn.microsoft.com/windows/apps/publish/publish-your-app/msi/create-app-submission
- Store submission FAQ: https://learn.microsoft.com/windows/apps/publish/faq/submit-your-app

## What blocks pure Store/MSIX

The current product expects:
- service registration
- privileged DNS changes
- machine-level networking policy changes

That does not fit a normal unelevated Store/MSIX app model.

## Installer-based Store track

Possible but risky:
- submit the Inno Setup installer as an MSI/EXE style Store app
- clearly disclose service/elevation behavior
- support silent install/uninstall
- ensure installer URL and binary are stable for certification
- avoid surprise behavior during install

## Recommended channels

Primary:
- portable zip
- installer

Secondary/future:
- Microsoft Store installer-based submission after installer hardening, signing, silent install verification, and policy review.

Do not create fake MSIX compatibility just to have a Store badge.
