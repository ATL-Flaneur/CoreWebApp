# CoreWebApp

This is a simple C# ASP.NET Core web app developed to show creation of web APIs, export of prometheus metrics, Dockerization of the app, and using Grafana to query Prometheus and display the metric in a Grafana dashboard panel.

These instructions assume the use of Ubuntu Linux but should work on other Debian-based distributions.

## Prerequisites

Prep the Linux machine with:

`sudo apt install -y dotnet-sdk-9.0`

## APIs

### /health

Returns a 200 status code if the app is running.

### /stats

Returns CPU usage, processor count, OS version, and number of users.

### /getusers

Returns the name and age of users who have been added to the system along with an integer indicating the user ID.

### /adduser

Add a user to the system with firstName, lastName, and age.

Example:

```
curl -X POST \
-H "Content-Type: application/json" \
-d '{"firstName":"John", "lastName":"Smith", "age":42}' \
http://localhost:5000/adduser
```

### /deluser

Remove a user with the specified user ID.

Example:

```
curl -X POST \
-H "Content-Type: application/json" \
-d '{"userId":0}' \
http://localhost:5000/deluser
```

## Docker

### Prep the machine

Install the Docker GPG key:

```
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg
```

Add the Docker repository:

```
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list
```

Update the package index:

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

sudo docker run -d -p 3000:3000 -p 5000:5000 -p 9090:9090 --name server-container server:1.0


```

Open a browser and navigate to http://localhost:5000/health or run `curl localhost:5000/health` to verify that the app is running.

## Prometheus

### Install Prometheus

```
sudo apt install prometheus -y
```

Open a browser and navigate to http://localhost:9090 to view the dashboard.

### Configuration

The default configuration file is at `/etc/prometheus/prometheus.yml`

Look for a `scrape_configs:` clause and change the `targets:` line to read: `targets: [localhost:5000]`.

Restart Prometheus with:

```
sudo systemctl restart prometheus
```

## Grafana

### Install Grafana 

Install the Grafana GPG key:

```
curl -fsSL https://apt.grafana.com/gpg.key | sudo gpg --dearmor -o /etc/apt/keyrings/grafana.gpg
```

Add the Grafana stable repository:

```
echo "deb [signed-by=/etc/apt/keyrings/grafana.gpg] https://apt.grafana.com stable main" | sudo tee -a /etc/apt/sources.list.d/grafana.list 

```

Update the package index:

```
sudo apt update
```

Install Grafana OSS. This is the free-to-use, open-source version released under the Apache 2.0 license.

```
sudo apt-get install -y grafana
```

### Start Grafana

Start Grafana:

```
sudo systemctl daemon-reload
sudo systemctl start grafana-server
```

Verify that Grafana is running:

```
sudo systemctl status grafana-server
```

### Connecting to Grafana

Open a browser and navigate to http://localhost:9090 to view the dashboard. The default username/password is `admin`/`admin`.

### Configuring Grafana to scrape Prometheus data

Open the Grafnana configuration file (substitute your favorite editor for `vi`):

```
sudo vi /etc/grafana/grafana.ini
```

Scroll down to `Internal Grafana Metrics` and uncomment (remove the semicolon) the lines:

```
enabled           = true
```

and

```
disable_total_stats = false
```

### Restart Grafana

```
sudo systemctl restart grafana-server
```

### Configure Prometheus to integrate with Grafana

Open the Prometheus configuration file (substitute your favorite editor for `vi`):

```
sudo vi /etc/prometheus/prometheus.yml 
```

Scroll down to the bottom and add a new job:

```
  - job_name: 'grafana_metrics'

    scrape_interval: 15s
    scrape_timeout: 5s

    static_configs:
      - targets: ['localhost:3000']
```

Verify the configuration file:

```
promtool check config /etc/prometheus/prometheus.yml
```

YAML files are very sensitive to indentation and alignment.

### Restart Prometheus

Restart Prometheus to reload configuration.

```
sudo systemctl restart prometheus
```

### Viewing metrics

