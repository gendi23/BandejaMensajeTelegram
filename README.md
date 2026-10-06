

## Levantar la solución

Desde la raíz del repositorio, una vez completado `.env`:

```powershell
docker compose up --build
```


Para detener los contenedores conservando los datos:

```powershell
docker compose down
```

Para eliminar también los volúmenes (borra mensajes y colas persistidos):

```powershell
docker compose down -v
```
