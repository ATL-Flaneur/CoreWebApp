# CoreWebApp

This is a simple C# ASP.NET Core web app developed to show creation of web APIs, export of prometheus metrics, Dockerization of the app, and using Grafana to query Prometheus and display the metric in a dashboard panel.


## APIs

### /health

Returns a 200 status code if the app is running.

### /stats

Returns CPU usage, processor count, OS version, and number of users.

### /getusers

Returns the name and age of users who have been added to the system.

### /addusers

Add a user to the system with firstName, lastName, and age.

Example:

```
curl -X POST \
-H "Content-Type: application/json" \
-d '{"firstName":"John", "lastName":"Smith", "age":42}' \
http://localhost:5062/adduser
```