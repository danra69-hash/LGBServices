# One-off: duplicate emails (different casing)

After the S1 email normalization fix, new users always store lowercased emails. Existing databases may still have pairs like `Foo@x.com` and `foo@x.com`.

Inspect (SQLite):

```sql
SELECT lower(Email) AS email_key, COUNT(*) AS n, GROUP_CONCAT(Email) AS variants
FROM Users
GROUP BY lower(Email)
HAVING n > 1;
```

Resolve manually (keep one row, reassign FKs, delete the other). Do not auto-merge in app code.
