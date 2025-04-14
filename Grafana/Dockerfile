# AI! Please add the Dockerfile instructions below to create a container that uses the dashbard files in this directory and ../dashboards/server_dashboard.json
                                                                                                                     
FROM grafana/grafana:latest                                                                                                   
                                                                                                                              
# Copy the dashboard JSON file into the container                                                                             
COPY ../dashboards/server_dashboard.json /var/lib/grafana/dashboards/server_dashboard.json                                    
                                                                                                                              
# Copy any other dashboard files from the current directory
COPY *.yml /var/lib/grafana/dashboards/

# Expose the Grafana port                                                                                                     
EXPOSE 3000
                                                                                                                              
# Start Grafana                                                                                                               
CMD ["grafana-server"]    