FROM grafana/grafana:latest

# Copy the dashboard JSON file into the container
COPY server_dashboard.json /etc/grafana/provisioning/dashboards/server_dashboard.json

# Copy any provisioning files (like datasources or dashboard provisioning) to the correct directory
COPY grafana-dashboard-provisioning.yml /etc/grafana/provisioning/dashboards/dashboard.yml
COPY grafana-datasource-provisioning.yml /etc/grafana/provisioning/datasources/datasource.yml

# Expose the Grafana port
EXPOSE 3000

# Start Grafana
CMD ["grafana-server"]
