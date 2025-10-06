# Azure Retail Hub ☁️

Azure Retail Hub is a comprehensive, admin-focused retail management web application built with **ASP.NET Core MVC (.NET 8)** and **Azure Functions (Isolated .NET)**. It manages customers, products, orders, and contracts, using Azure Storage services for a scalable, cost-effective backend.

**Live site:** https://st10447759.azurewebsites.net/  
**function site:** https://st10447759-func.azurewebsites.net/ 
**Demo video:** 

---

## What’s new in Part 2 ✅

- **Serverless functions** to handle:
  - Product image upload to **Blob Storage**
  - Product upsert to **Table Storage**
  - Customer upsert/delete to **Table Storage**
  - Contract upload to **File Share**
  - Order enqueue via **Queue Storage** and a **queue trigger** to process orders
- **Decoupled order pipeline:** MVC enqueues → Azure Functions consume → Tables updated
- **Improved UX** on create/edit pages (Bootstrap styling)
- **Deployment** to Azure App Service + Function App
- **Production-safe configuration** using App Service/Function App **Application settings** (no secrets in code)

---

## Features ✨

- **Customer Management** — full CRUD
- **Product Management** — CRUD with image upload to Blob Storage
- **Order Processing** — create orders, update status; backend events via Queue Storage
- **Contracts** — upload/download/delete PDF/docs to Azure File Share
- **Asynchronous architecture** — order creation is non-blocking; processing handled by Functions

---

## Architecture 🧩

- **ASP.NET Core MVC (Web App)** — admin interface  
- **Azure Functions (Isolated .NET)** — HTTP + QueueTrigger  
- **Azure Storage**
  - **Table Storage:** `customers`, `products`, `orders`
  - **Blob Storage:** `productimages`
  - **Queue Storage:** `orderqueue` (+ poison queue)
  - **File Share:** `contracts`

High-level flow:  
1. Admin creates/edits products/customers (MVC → Functions)  
2. Product images go to Blob Storage; metadata to Table Storage  
3. Placing an order enqueues a message to `orderqueue`  
4. **OrdersQueueProcessor** (Function) consumes messages and upserts/deletes orders in Table Storage

---

## Azure Functions Endpoints 🔌

> Base URL (prod): `https://st10447759-func.azurewebsites.net/api`  
> In local dev, Functions run on `http://localhost:<port>/api`

- `POST /products/image` → **ProductImageUpload**: multipart form (`imageFile`) → Blob URL
- `POST|PUT /products` → **ProductUpsert**: upserts product entity (Table)
- `POST|PUT /customers` → **CustomerUpsert**: upserts customer entity (Table)
- `DELETE /customers/{id}` → **CustomerDelete**
- `POST /contracts` → **ContractUpload**: multipart form (`file`) → Azure File Share
- `POST /orders/enqueue` → **OrdersEnqueue**: sends order messages to Queue
- **QueueTrigger** → **OrdersQueueProcessor**: reads `orderqueue`, updates Table

> The MVC app calls these via `FunctionApi:BaseUrl` + `FunctionApi:Key` (sent as `code=<key>` query).

---

## Technology Stack 🛠️

- **Backend:** C#, ASP.NET Core MVC (.NET 8), Azure Functions (Isolated)
- **Frontend:** Razor, Bootstrap 5
- **Storage:** Azure Tables, Blobs, Queues, Files
- **Dev:** Visual Studio 2022

---

## Configuration 🔐

### MVC `appsettings.json` (no secrets)
```json
{
  "StorageOptions": {
    "CustomersTable": "customers",
    "ProductsTable": "products",
    "OrdersTable": "orders",
    "BlobContainer": "productimages",
    "QueueName": "orderqueue",
    "FileShareName": "contracts"
  },
  "FunctionApi": {
    "BaseUrl": "https://st10447759-func.azurewebsites.net/api"
  }
}
```

> **Secrets (DO NOT COMMIT):**
> - In **Azure Portal → Web App → Configuration** add:
>   - `StorageOptions:ConnectionString` = your storage connection string
>   - `FunctionApi:Key` = your function host key
>
> - In **Function App → Configuration** add:
>   - `StorageOptions:ConnectionString`
>   - `StorageOptions:ProductsTable`, `CustomersTable`, `OrdersTable`, `BlobContainer`, `QueueName`, `FileShareName` (match MVC)
>   - `AzureWebJobsStorage` (default storage for Functions)

This keeps GitHub push-protection happy and your keys safe.

---

## Getting Started (Local) 🏁

**Prereqs:** .NET 8 SDK, Azure Functions Core Tools v4, Azure Storage account

1) **Run Functions locally**
```bash
cd Functions/AzureRetailHub.Functions
func start
```
You’ll see function URLs in the console.

2) **Run MVC locally** (F5 in Visual Studio).

3) **Test flow**
- Create a **product** (upload image) → Blob URL appears in product list.
- Create a **customer**.
- Create an **order** → watch Functions console: `OrdersEnqueue` then `OrdersQueueProcessor`.
- Change **order status** in MVC → verify row updates in Table Storage.

---

## Deployment 🌐

- **MVC** → Azure App Service  
  - Publish from VS → select existing App Service  
  - Portal → Web App → Configuration → set **Application settings** listed above
- **Functions** → Azure Function App  
  - Publish from VS → select existing Function App  
  - Portal → Function App → Configuration → set **Application settings** (including `AzureWebJobsStorage`)

---

## (Part 2) Services for Improving Customer Experience 💡

### Event Hubs vs Event Bus (and where our app fits)

**Event Hub (Azure Event Hubs)**  
- **What it is:** Big-data, high-throughput event ingestion service (millions of events/sec).  
- **Use:** Telemetry, clickstreams, IoT device data; consumers read via partitions/checkpoints.  
- **Benefit to CX:** Real-time analytics (stock trends, popular items), anomaly detection (sudden spikes), and faster insight-driven UX.

**Event Bus (e.g., Service Bus Topics or a lightweight in-app bus)**  
- **What it is:** Message distribution with routing (topics/subscriptions), durable delivery, DLQ.  
- **Use:** Business workflows and integration (order placed → notify warehouse, email, billing), guaranteed delivery.  
- **Benefit to CX:** Reliable downstream actions (confirmations, shipping, restocks) happen fast and consistently, reducing delays and errors.

**Our app today:** Uses **Azure Queue Storage** (simple FIFO) for decoupled order processing.  
**If we upgraded:**  
- Use **Service Bus (Event Bus)** for richer routing and multiple subscribers (warehouse, email, ERP).  
- Use **Event Hubs** for **analytics** (stream events → Stream Analytics / Synapse → dashboards showing real-time demand).  
Together, these improve customer experience with **speed, reliability, and insight**.

---

## Screenshots to Include for Part 2 🖼️

1. **Azure Portal – Resource Group** showing Web App + Function App + Storage account  
2. **Web App → Configuration** (Application settings) with keys names (values hidden)  
3. **Function App → Functions list** showing all functions (ProductImageUpload, ProductUpsert, CustomerUpsert, CustomerDelete, OrdersEnqueue, OrdersQueueProcessor)  
4. **Function App → Monitor (or Logs)** showing successful invocations of:
   - `ProductImageUpload`
   - `OrdersEnqueue` and **OrdersQueueProcessor** trigger  
5. **Storage Account**
   - **Tables**: `customers`, `products`, `orders` with sample rows  
   - **Containers**: `productimages` with uploaded image  
   - **Queues**: `orderqueue` and (optionally) `-poison`  
   - **File shares**: `contracts` with uploaded file  
6. **Running MVC pages**
   - Create Product (with file picker), Product list with image  
   - Create Customer  
   - Create Order (select customer + items)  
   - Change Order Status page (successful update)  
7. **GitHub repo** showing that `appsettings.json` has no secrets (and a successful push)

---

## Testing Checklist ✅

- [ ] Create product with image → Blob URL displays in list  
- [ ] Create customer → appears in `customers` table  
- [ ] Create order → message in queue → OrdersQueueProcessor updates `orders` table  
- [ ] Update status from MVC → refresh list shows new status  
- [ ] Upload a contract → appears in File Share; download/delete works  
- [ ] Deleting a product/customer behaves as expected  
- [ ] All functions reachable with valid `FunctionApi:Key`  

---

## Security Notes 🔒

- No secrets committed. All keys managed in App/Function **Application settings**.  
- Requests to Functions include the **function key** (`?code=...`).  
- Avoid exposing blob containers publicly unless intended (use SAS or private + serve via app).

---

## Troubleshooting 🧯

- **Function says “Base-64 invalid” on queue:** ensure MVC enqueues **raw JSON** and processor **does NOT** Base64-decode unless you encoded it first.  
- **Image upload fails (Content-Length):** parse multipart properly and pass stream directly to `BlobClient.UploadAsync`.  
- **GitHub push blocked:** strip secrets from history, then force-push (`git filter-repo` + `push --force-with-lease`).

---

## Note on AI Assistance 🤖

AI tools helped with boilerplate, refactors, and this README. Core logic, Azure integration, and debugging were implemented and verified by the developer.

---

## License

Distributed under the MIT License. See `LICENSE.txt`.
