FROM grafana/grafana:latest

# Copy the dashboard JSON file into the container
COPY ../dashboards/server_dashboard.json /var/lib/grafana/dashboards/server_dashboard.json

# Copy any provisioning files (like datasources or dashboard provisioning) to the correct directory
COPY *.yml /etc/grafana/provisioning/

# Expose the Grafana port
EXPOSE 3000

# Start Grafana
CMD ["grafana-server"]
