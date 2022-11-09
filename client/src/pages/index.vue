<template>
  <v-container class="home-container px-10 pt-10">
    <v-row justify="center" align="start" class="mb-5">
      <v-col cols="12" class="d-flex justify-center">
        <v-img
          :src="require('~/static/logo.svg')"
          alt="WebAuthn Logo"
          height="75"
          width="75"
          contain
        ></v-img>       
      </v-col>
    </v-row>
    <v-row justify="center" align="start">
      <v-col cols="12" md="6" lg="5"  class="d-flex flex-column align-stretch">
        <v-card
          class="pa-5 mb-5"
        >
          <v-card-title>
            <v-avatar
              color="primary"
              size="75"
              class="mr-5"
            >{{user.firstName.substring(0, 1)}}{{user.lastName.substring(0, 1)}}</v-avatar>

            <span>{{user.firstName}} {{user.lastName}}</span>

            <v-spacer></v-spacer>

            <v-menu
              bottom
              left
            >
              <template v-slot:activator="{ on, attrs }">
                <v-btn
                  dark
                  icon
                  v-bind="attrs"
                  v-on="on"
                >
                  <v-icon>mdi-dots-vertical</v-icon>
                </v-btn>
              </template>

              <v-list>
                <v-list-item @click="onLogoutClick">
                  <v-list-item-icon>
                    <v-icon>mdi-logout</v-icon>
                  </v-list-item-icon>
                  <v-list-item-title>
                    Log out
                  </v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>
          </v-card-title>
        </v-card>
        <UserLoginDevicesCard></UserLoginDevicesCard>
      </v-col>
    </v-row>
  </v-container>
</template>

<script>
export default {
  name: 'IndexPage',
  async asyncData({ $axios }) {
    return $axios
      .get('api/users/me')
      .then((user) => { user });
  },
  methods: {
    onLogoutClick() {
      this.$axios.$post('/api/webauthn/logout');
      this.$router.push({
        to: '/login'
      });
    }
  }
};
</script>

<style scoped>
  .home-container {
    background-color: #fafafa; 
  }
</style>