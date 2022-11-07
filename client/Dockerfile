#TODO:Couldnt get nuxt dev server working with v18 LTS node
FROM node:16-alpine as run

ARG NODE_ENV=development
ENV NODE_ENV=$NODE_ENV

ARG API_URL
ENV API_URL=$API_URL

# do dependency installation as separate step for caching
WORKDIR /app
COPY ./package*.json ./
RUN ["npm", "install", "--force"]

# copy everything else and run app
COPY ./ ./

CMD ["npm", "run", "dev"]


FROM node:16-alpine as generate

ARG NODE_ENV=production
ENV NODE_ENV=$NODE_ENV

ARG API_URL
ENV API_URL=$API_URL

WORKDIR /app
COPY --from=run /app ./
RUN ["npm", "run", "generate"]


FROM nginx
COPY --from=generate /app/dist /usr/share/nginx/html