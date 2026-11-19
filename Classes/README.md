# Shared classes

Host-independent classes used across frontend/backend belong here. `NavProIdentity.cs` centralizes the product/tool labels and stable plugin registration ID.

Keep operation-specific data in `Backend/Models`, pure policies in `Backend/Core`, Autodesk API operations in `Backend/Services`, and Windows controls in `Frontend`. Do not move every C# class into this folder: classes should stay with their responsibility.
