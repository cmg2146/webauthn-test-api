#TODO:Couldnt get nuxt dev server working with v18 LTS node
FROM node:16-alpine as install-deps

RUN apk add --no-cache bash

# do dependency installation as separate step for caching
WORKDIR /app
COPY ./package*.json ./
RUN ["npm", "install", "--force"]
#copy all the rest of the files needed to run app
#TODO: development: bind mount this instead for hot reloading
COPY ./ ./

#No CMD or entrypoint. In development, docker compose targets this stage and
#specifies the command. In production, we fall through to next stage.


FROM node:16-alpine as generate

ARG NODE_ENV=production
ENV NODE_ENV=$NODE_ENV

ARG API_URL
ENV API_URL=$API_URL

WORKDIR /app
COPY --from=install-deps /app ./

RUN ["npm", "run", "generate"]


FROM nginx
COPY --from=generate /app/dist /usr/share/nginx/html