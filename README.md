# AcxiomCRM

A role-based Customer Relationship Management (CRM) web application built with **ASP.NET Core MVC, Entity Framework Core, PostgreSQL, and ASP.NET Core Identity**.

## Development Note

This project was developed as part of a time-bound implementation task.

The available implementation window was limited, and the development period was approximately **7:00 PM to 12:00 AM**. Because of this constraint, the project focuses on implementing the core CRM functionality and security foundations first.

I have intentionally documented the current implementation status below rather than representing unfinished functionality as completed.

This README is provided for transparency and to make it clear which parts of the requested specification have been implemented and which parts would require additional development time.

## Implemented Modules

### Authentication & Authorization

* ASP.NET Core Identity
* Registration and Login
* Password policy
* Account lockout configuration
* Role-based authorization
* Roles:

  * Admin
  * Manager
  * SalesExecutive
* Secure logout
* Anti-forgery protection

### Dashboard

* Role-scoped dashboard
* Customer and Lead KPIs
* Opportunity KPIs
* Pipeline value
* Lead status chart
* Opportunity pipeline chart
* Monthly opportunity outcomes
* Date filters:

  * Today
  * This Week
  * This Month
  * Custom Range

### Customer Management

* Customer creation
* Customer listing
* Search
* Details
* Edit
* Delete
* Server-side validation
* Duplicate email/phone checks

### Lead Management

* Lead CRUD
* Search
* Status management
* Lead assignment
* Lead-to-Customer/Opportunity conversion

### Opportunity Management

* Opportunity CRUD
* Pipeline stages
* Amount and probability validation
* Weighted pipeline calculation
* Expected close date validation
* Assignment and role-based access

### Follow-Up Management

* Follow-up CRUD
* Customer/Lead association
* Status management
* Date validation
* Assignment and role-based access
* Search/filter support

### Activity Management

* Activity CRUD
* Call, Meeting, Email and Task support
* Customer/Lead association
* Status and date handling
* Role-based access

### User & Role Management

* Admin-only user management
* User search
* User creation
* User editing
* Role assignment
* Account lock/unlock controls
* Protection against removing the last Admin account
* ASP.NET Core Identity used for password handling

### Audit Log Foundation

* Audit log database model
* Audit service
* User/action/entity/record tracking structure
* Database migration
* Security-conscious handling of audit data

### REST API

* Secured Customer REST API
* GET, POST, PUT and DELETE endpoints
* DTO-based request/response handling
* Role-based access
* Duplicate conflict handling

## Remaining / Further Work

The following areas would require additional development and testing time to achieve complete production-level alignment with the full specification:

* Complete Audit Log event integration across all CRM operations
* Admin Audit Log viewing/filtering UI
* Complete Reports module
* Additional REST APIs for Leads, Opportunities and Follow-Ups
* Additional validation and edge-case testing
* Full end-to-end acceptance testing against every PRD scenario
* Further refinement of role-specific Manager/team data scope where required
* Production hardening and deployment configuration

## Technology Stack

* **Backend:** ASP.NET Core MVC
* **Language:** C#
* **ORM:** Entity Framework Core
* **Database:** PostgreSQL
* **Authentication:** ASP.NET Core Identity
* **Frontend:** Razor Views + Bootstrap
* **Charts:** Chart.js
* **Authorization:** ASP.NET Core Role-Based Authorization
* **Version Control:** Git / GitHub

## Security Practices

The application follows security-oriented implementation practices including:

* ASP.NET Core Identity password hashing
* Password policy enforcement
* Account lockout
* Server-side authorization
* Role-based access control
* Anti-forgery protection for state-changing MVC requests
* DTOs for REST API boundaries
* Server-generated identifiers and timestamps where appropriate
* Protection against overposting through dedicated view models
* No exposure of passwords, password hashes or security tokens in the UI/API

## Repository

The complete source code and development history are available in the GitHub repository:

https://github.com/saivamsi-dev/AcxiomCRM

## Note to Reviewer

The implementation status in this README is intentionally transparent. Features listed as remaining are not represented as completed. Given the constrained implementation window, the project prioritizes a functional CRM foundation, authentication and authorization, core CRM workflows, dashboard functionality, user/role management, audit infrastructure, and a secured REST API.

Additional time would be used to complete the remaining reporting, audit UI/event coverage, API expansion, and comprehensive acceptance testing.
