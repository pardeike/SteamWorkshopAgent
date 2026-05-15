# Architecture Enhancements

Future SteamWorkshopAgent work should treat Steam Workshop automation as a hybrid system rather than a single publishing backend.

## Current Backend

The current implementation uses SteamCMD:

```text
steamcmd +login <user> +workshop_build_item <workshop.vdf> +quit
```

This is useful because it is simple, scriptable, and officially documented for Workshop item creation and updates. It can publish content, preview image, title, description, visibility, and changenote through the generated VDF.

Its main weaknesses are:

- It uses SteamCMD's own login token cache, not the already-authenticated Steam desktop client session.
- It is poor at reading current Workshop state.
- It has no natural structured read path for page stats, current tags, current previews, current rendered description, or previous changenotes.
- Tag handling through VDF is less explicit than direct `ISteamUGC.SetItemTags`, so the current server intentionally preserves existing tags.

Keep SteamCMD as a fallback backend, but do not make it the only long-term path.

## Steamworks Helper Backend

RimWorld's own Workshop uploader does not use SteamCMD. Decompilation of the current RimWorld `Assembly-CSharp.dll` shows it uses Steamworks UGC calls from inside the running game process:

```text
SteamUGC.CreateItem(...)
SteamUGC.StartItemUpdate(...)
SteamUGC.SetItemTitle(...)
SteamUGC.SetItemDescription(...)
SteamUGC.SetItemPreview(...)
SteamUGC.SetItemTags(...)
SteamUGC.SetItemContent(...)
SteamUGC.SubmitItemUpdate(...)
```

This means RimWorld inherits the logged-in Steam desktop client session, which is why it normally does not ask for Steam credentials.

A future preferred publish backend should be a small local Steamworks uploader helper that:

- initializes Steamworks for RimWorld app id `294100`;
- uses the active Steam client session instead of SteamCMD credentials;
- accepts staged content, preview path, title, description policy, tags, metadata, visibility, and changenote from the MCP server;
- uses `ISteamUGC.StartItemUpdate` / `SubmitItemUpdate`;
- pumps Steam callbacks until completion;
- reports upload progress through `GetItemUpdateProgress`;
- returns detailed Steam result codes and whether the user needs to accept the Workshop legal agreement.

Expected advantages:

- No separate SteamCMD login setup when Steam is already authenticated.
- Cleaner support for tags through `SetItemTags`.
- Cleaner support for key-value tags and metadata through `AddItemKeyValueTag`, `RemoveItemKeyValueTags`, and `SetItemMetadata`.
- Cleaner support for additional previews and videos through the `AddItemPreview*`, `UpdateItemPreview*`, and `RemoveItemPreview` calls.
- Better parity with RimWorld's built-in Workshop behavior.

Expected costs:

- More implementation work than SteamCMD.
- Needs Steamworks redistributables and careful app id setup.
- Needs reliable callback handling, timeout behavior, and result normalization.
- Needs testing around Steam not running, wrong account, legal agreement required, app license missing, and invalid Workshop ownership.

## Read And Stats Backend

Publishing and reading should be separated.

For current Workshop state, use public Steam Web APIs and targeted page reads before reaching for either SteamCMD or Steamworks.

Candidate read tools:

- `workshop_item_details`: fetch structured published-file details by Workshop id.
- `workshop_page_snapshot`: fetch the human-visible Workshop page and extract rendered title, description, tags, preview URLs, and visible counters where available.
- `workshop_compare_release_to_workshop`: compare a GitHub release plan against current Workshop state.
- `workshop_verify_after_publish`: confirm that last-updated fields, title, visible description policy, and expected metadata changed after an upload.

Useful Steam Web API surfaces:

- `ISteamRemoteStorage.GetPublishedFileDetails` for basic item metadata by `publishedfileid`.
- `IPublishedFileService.QueryFiles` for richer query results, including options for vote data, tags, key-value tags, previews, children, metadata, and playtime stats.

Some `IPublishedFileService` methods require a Steamworks publisher key and must be treated as sensitive/server-side operations. Examples include developer metadata, tags, ban status, and incompatible status updates.

## Changenote History Caveat

Both SteamCMD and Steamworks can submit a changenote, but neither appears to provide a clean, stable public API for reading the full prior changenote history of a Workshop item.

Treat changenote-history reads as best-effort:

- try public Workshop page parsing first;
- avoid depending on Steam page markup for critical publish correctness;
- record submitted changenotes locally in `~/Library/Application Support/SteamWorkshopAgent/runs/.../plan.json`;
- compare future GitHub release notes against locally recorded submissions when available.

## Recommended Direction

The long-term architecture should be:

1. Keep SteamCMD as a documented fallback publisher.
2. Add non-mutating Workshop read tools using Web API / page snapshots.
3. Add a Steamworks helper as the preferred local publisher.
4. Use read tools before and after publishing to validate expected Workshop state.
5. Keep destructive or sensitive operations behind explicit confirmation flags.

The target operator experience should be:

```text
Plan:
  read local mod metadata
  read GitHub release
  read current Workshop state
  show exact differences

Publish:
  build release content
  upload through preferred backend
  submit changenote
  preserve or update description/tags according to explicit policy

Verify:
  read Workshop state again
  report whether the public page matches the plan
  persist local publish evidence
```

## References

- Steam Workshop implementation guide: https://partner.steamgames.com/doc/features/workshop/implementation
- `ISteamUGC` API: https://partner.steamgames.com/doc/api/ISteamUGC
- `ISteamRemoteStorage.GetPublishedFileDetails`: https://partner.steamgames.com/doc/webapi/ISteamRemoteStorage
- `IPublishedFileService.QueryFiles`: https://partner.steamgames.com/doc/webapi/IPublishedFileService
