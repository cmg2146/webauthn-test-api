<template>
  <v-container class="px-10 pt-10">
    <v-row justify="center" align="start" class="mb-5">
      <v-col cols="12" md="7" class="d-flex justify-center">
        <v-img
          :src="require('~/static/logo.svg')"
          alt="WebAuthn Logo"
          height="75"
          width="75"
          contain
        ></v-img>
      </v-col>
    </v-row>
    <v-row justify="center" align="start" class="mb-5">
      <v-col cols="12" md="7" class="d-flex align-center">
        <v-avatar
          color="primary"
          size="75"
          class="mr-5 text-h5"
        >{{user.firstName.substring(0, 1)}}{{user.lastName.substring(0, 1)}}</v-avatar>
        <span class="font-weight-bold text-h5">
          {{user.firstName}} {{user.lastName}}
        </span>

        <v-spacer></v-spacer>

        <v-btn
          text
          @click="onLogoutClick"
        >
          <v-icon>mdi-logout</v-icon>
          Logout
        </v-btn>
      </v-col>
    </v-row>    
    <v-row justify="center" align="start">
      <v-col cols="12" md="7"  class="d-flex flex-column align-stretch">
        <UserLoginDevicesCard></UserLoginDevicesCard>
      </v-col>
    </v-row>
  </v-container>
</template>

<script>
export default {
  name: 'IndexPage',
  data() {
    return {
      user: {
        firstName: this.$auth.user.firstName,
        lastName: this.$auth.user.lastName,
        displayName: this.$auth.user.displayName
      }
    };
  },
  methods: {
    onLogoutClick() {
      this.$auth.logout();
    }
  }
};
</script>