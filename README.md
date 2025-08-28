

# Azure Retail Hub ☁️

Azure Retail Hub is a comprehensive, admin-focused retail management web application built with ASP.NET Core MVC and deeply integrated with Microsoft Azure. It provides a centralized platform for managing customers, products, orders, and contracts, leveraging the power of various Azure Storage services for a scalable and robust backend.

This project serves as a practical demonstration of cloud application development, showcasing how to build a full-stack web app that utilizes a suite of cloud services for different data persistence needs.

-----

## Features ✨

  * **Customer Management:** Full CRUD (Create, Read, Update, Delete) functionality for customer records.
  * **Product Management:** Manage a product catalog with details like name, description, price, and an image. Includes full CRUD capabilities.
  * **Image Handling:** Product images are uploaded and stored securely in Azure Blob Storage, with old images being automatically deleted upon replacement.
  * **Order Processing:** Create detailed orders by selecting existing customers and adding multiple products. Order status can be tracked and updated.
  * **Decoupled Architecture:** On order creation, a message is sent to Azure Queue Storage, allowing for asynchronous processing or integration with other systems.
  * **Contract Management:** A user-friendly interface for uploading, downloading, and deleting contract documents, powered by Azure File Storage.

-----

## Technology Stack 🛠️

  * **Backend:** C\#, ASP.NET Core MVC (.NET 8)
  * **Frontend:** HTML, CSS, JavaScript, Bootstrap 5
  * **Database/Storage:** Microsoft Azure Storage Services
  * **IDE:** Visual Studio 2022

-----

## Azure Services Used 🚀

This application is built around four key Azure Storage services, each chosen for a specific purpose:

1.  **Azure Table Storage:** A NoSQL key-value store used for structured data.
      * **Use Case:** Stores all primary records for `Customers`, `Products`, and `Orders`. It's cost-effective and highly scalable for simple, structured data.
2.  **Azure Blob Storage:** Object storage for unstructured data.
      * **Use Case:** Stores all product images. Blobs are perfect for binary data like images or videos, and can be configured for public access to be displayed directly on the website.
3.  **Azure Queue Storage:** A messaging service for asynchronous communication.
      * **Use Case:** When a new order is created, a message containing the order details is sent to a queue. This decouples the initial order creation from any subsequent processing (like notifying a warehouse), making the application more resilient.
4.  **Azure File Storage:** A fully managed file share service.
      * **Use Case:** Manages the upload, download, and deletion of contract documents. It provides a simple and effective way to handle file-based data in the cloud.

-----

## Getting Started 🏁

To get a local copy up and running, follow these steps.

### Prerequisites

  * [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
  * [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)
  * An active [Microsoft Azure Subscription](https://azure.microsoft.com/free/)

### Installation

1.  **Clone the repo**
    ```sh
    git clone https://github.com/your_username/AzureRetailHub.git
    ```
2.  **Navigate to the project directory**
    ```sh
    cd AzureRetailHub
    ```
3.  **Set up Azure Storage**
      * Create a new Azure Storage Account in the Azure Portal.
      * Copy the connection string for your storage account.

### Configuration

1.  Open the solution in Visual Studio.
2.  In the `AzureRetailHub` project, open the `appsettings.json` file.
3.  Update the `StorageOptions` section with your Azure Storage connection string and desired names for your tables, container, queue, and file share.
    ```json
    "StorageOptions": {
      "ConnectionString": "YOUR_CONNECTION_STRING_HERE",
      "CustomersTable": "customers",
      "ProductsTable": "products",
      "OrdersTable": "orders",
      "BlobContainer": "productimages",
      "QueueName": "orderqueue",
      "FileShareName": "contracts"
    }
    ```
4.  Save the file.

### Usage

1.  Press `F5` or click the "Start Debugging" button in Visual Studio to launch the application.
2.  Your browser will open to the application's home page.
3.  Navigate through the "Customers," "Products," "Orders," and "Contracts" sections to test the application's functionality. The required tables, container, queue, and file share will be created automatically in your Azure Storage account the first time they are accessed.

-----

## Note on AI Assistance 🤖

During the development of this project, AI-powered tools were utilized as a productivity aid. Assistance was primarily sought for:

  * **Boilerplate Code Generation:** Generating initial controller actions and view models based on existing patterns.
  * **Code Refinement:** Suggesting improvements for code structure and logic, such as the method for safely extracting a blob name from a URL.
  * **Documentation:** Assisting in the generation of comments, summaries, and this README file to clearly explain the project's features and architecture.

The core logic, application structure, and integration of Azure services were designed and implemented by the developer.

-----

## Contributing

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".

1.  Fork the Project
2.  Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3.  Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4.  Push to the Branch (`git push origin feature/AmazingFeature`)
5.  Open a Pull Request

-----

## License

Distributed under the MIT License. See `LICENSE.txt` for more information.
