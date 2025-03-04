# CoreWebApp

This is a simple C# ASP.NET Core web app developed to show creation of web APIs, export of Prometheus metrics, Dockerization of the app, and using Grafana to query Prometheus and display the metrics on dashboard.

These instructions assume the use of Ubuntu Linux but should work on other Debian-based distributions.

## Prerequisites

Install .NET 9 machine with:

`sudo apt install -y dotnet-sdk-9.0`

See the Docker section installing packages to build/run a Dockerized version.

## APIs

### /health

Returns a 200 status code if the app is running.

Example:

```
curl -X GET http://localhost:5000/health 
```

### /metrics

Returns Prometheus metrics.

Custom metrics include:

* `successful_user_adds`: Number of successful user adds (counter)
* `failed_user_adds`: Number of failed user adds (counter)
* `successful_user_deletes`: Number of successful user deletes (counter)
* `failed_user_deletes`: Number of failed user deletes (counter)
* `successful_user_clears`: Number of successful user deletes (counter)
* `failed_user_clears`: Number of failed user deletes (counter)
* `num_active_users`: Number of users (gauge)

Example:

```
curl -X GET http://localhost:5000/metrics 
```

Best queried from Prometheus or Grafana!

### /stats

Returns CPU usage, processor count, OS version, and number of users.

Example:

```
curl -X GET http://localhost:5000/stats 
```

### /getusers

Returns the name and age of users who have been added to the system along with an integer user ID.

Example:

```
curl -X GET http://localhost:5000/getusers 
```

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

### /clearusers

Clear the user list. Pass in the current number of users via NumUsers as a safety check.

Example (currently 10 users):

```
curl -X POST \
-H "Content-Type: application/json" \
-d '{"numUsers":10}' \
http://localhost:5000/clearusers
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

### Make sure that the login user can run Docker.

  338  getent group docker
  339  sudo usermod -aG docker ayank

### Build the container.

```
cd Server
sudo docker build -t server:latest .
```

### Run the container mapping API and Prometheus ports.

Note that Dockerfile specifies use of port 8080 instead of 5000. This clarifies whether a connection is made to a debug instance or a Docker image.

```
docker run -d -p 8080:8080 --name server-container server:1.0
```

Open a browser and navigate to http://localhost:8080/health or run `curl localhost:8080/health` to verify that the app is running.



## Prometheus

### Install Prometheus

```
sudo apt install prometheus -y
```

Open a browser and navigate to http://localhost:9090 to view the dashboard and available metrics.

### Configuration

The default configuration file is at `/etc/prometheus/prometheus.yml`

Look for the `scrape_configs:` clause and add debug and Docker jobs:

```
scrape_configs:

  - job_name: 'server_debug'
    scrape_interval: 5s
    scrape_timeout: 5s

    static_configs:
      - targets: ['localhost:5000']

  - job_name: 'server_docker'
    scrape_interval: 5s
    scrape_timeout: 5s

    static configs:
      - targets: ['localhost:8080']
```

This points Prometheus at both instances for attaching to a local Docker container. A variable in the Grafana dashboard will allow switching between them.

Check config file syntax with:

```
promtool check config /etc/prometheus/prometheus.yml
```

Fix any mistakes then restart Prometheus:

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

### Configure Grafana to generate data for Prometheus

Open the Grafnana configuration file (substitute your favorite editor for `vi`):

```
sudo vi /etc/grafana/grafana.ini
```

Scroll down to `Internal Grafana Metrics` and uncomment (remove the semicolon) the lines:

```
enabled = true
```

and

```
disable_total_stats = false
```

### Restart Grafana

```
sudo systemctl restart grafana-server
```

### Configure Prometheus to scrape Grafana data (optional)

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

Fix any mistakes then restart Prometheus:

```
sudo systemctl restart prometheus
```

### Restart Prometheus

Restart Prometheus to reload configuration.

```
sudo systemctl restart prometheus
```

### Viewing metrics

