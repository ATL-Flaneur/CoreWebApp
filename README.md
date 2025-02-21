# CoreWebApp

This is a simple C# ASP.NET Core web app developed to show creation of web APIs, export of prometheus metrics, Dockerization of the app, and using Grafana to query Prometheus and display the metric in a dashboard panel.

## Prerequisites

Prep the Linux machine with:

`sudo apt install -y dotnet-sdk-9.0`

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

## Docker

### Prep the machine

Install Docker GPG key:

```
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg
```

Add Docker repository:

```
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
```

Update package index:

```
sudo apt update
```

Install Docker engine:

```
sudo apt install -y docker-ce docker-ce-cli containerd.io
```

### Build the container

```
cd Server
sudo docker build -t server:1.0 .
```

### Run the container

```
sudo docker run -d -p 5000:8080 --name server-container server:1.0
```

Open a browser and navigate to http://localhost:5000/health or run `curl localhost:5000/health` to verify that the app is running.

## Prometheus

### Install Prometheus

```
sudo apt install prometheus -y
```

Open abrowser and navigate to http://localhost:9090 to view the dashboard.

### Configuration

The default configuration file is at `/etc/prometheus/prometheus.yml`

Look for a `scrape_configs:` clause and change the `targets:` line to read: `targets: [localhost:5000]`.

Restart Prometheus with:

```
sudo systemctl restart prometheus
```